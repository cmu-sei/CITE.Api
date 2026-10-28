// Copyright 2022 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license, please see LICENSE.md in the project root for license information or contact permission@sei.cmu.edu for full terms.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Threading;
using System.Threading.Tasks;
using Cite.Api.Data;
using Cite.Api.Data.Enumerations;
using Cite.Api.Services;
using Cite.Api.Infrastructure.Authorization;
using Cite.Api.Infrastructure.Options;
using Cite.Api.ViewModels;

namespace Cite.Api.Hubs
{
    [Authorize(AuthenticationSchemes = "Bearer")]
    public class MainHub : Hub
    {
        private readonly ITeamService _teamService;
        private readonly IEvaluationService _evaluationService;
        private readonly IScoringModelService _scoringModelService;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly CancellationToken _ct;
        private readonly ICiteAuthorizationService _authorizationService;
        public const string ADMIN_DATA_GROUP = "AdminDataGroup";
        public const string EVALUATION_GROUP = "AdminEvaluationGroup";
        public const string SCORING_MODEL_GROUP = "AdminScoringModelGroup";
        public const string GROUP_GROUP = "AdminGroupGroup";
        public const string ROLE_GROUP = "AdminRoleGroup";
        public const string USER_GROUP = "AdminUserGroup";
        public const string OFFICIAL_SCORE_POSTFIX = "OfficialScore";
        public const string PAST_OFFICIAL_SCORE_POSTFIX = "PastOfficialScore";
        public const string ALL_MOVES_POSTFIX = "AllMoves";

        public MainHub(
            ITeamService teamService,
            IEvaluationService evaluationService,
            IScoringModelService scoringModelService,
            IServiceScopeFactory scopeFactory,
            ICiteAuthorizationService authorizationService
        )
        {
            _teamService = teamService;
            _evaluationService = evaluationService;
            _scoringModelService = scoringModelService;
            _scopeFactory = scopeFactory;
            CancellationTokenSource source = new CancellationTokenSource();
            _ct = source.Token;
            _authorizationService = authorizationService;
        }

        public async Task Join()
        {
            var idList = await GetIdList();
            foreach (var id in idList)
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, id.ToString());
            }
        }

        public async Task Leave()
        {
            var idList = await GetIdList();
            foreach (var id in idList)
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, id.ToString());
            }
        }

        public async Task SwitchTeam(Guid[] args)
        {
            if (args.Count() == 2)
            {
                var oldTeamId = args[0];
                var newTeamId = args[1];
                // leave every group the old team could have added, whatever the caller can do now
                var idList = await GetTeamIdList(oldTeamId, checkAccess: false);
                foreach (var id in idList)
                {
                    await Groups.RemoveFromGroupAsync(Context.ConnectionId, id);
                }
                // join the new team's groups the caller is allowed to read
                idList = await GetTeamIdList(newTeamId, checkAccess: true);
                foreach (var id in idList)
                {
                    await Groups.AddToGroupAsync(Context.ConnectionId, id);
                }
            }
        }

        public async Task JoinAdmin()
        {
            var idList = await GetAdminIdList();
            foreach (var id in idList)
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, id.ToString());
            }
        }

        public async Task LeaveAdmin()
        {
            var idList = await GetAdminIdList();
            foreach (var id in idList)
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, id.ToString());
            }
        }

        private async Task<List<string>> GetIdList()
        {
            // only add the user ID
            // evaluation and team will be added in the setTeam method
            var idList = new List<string>();
            var userId = Context.User.Identities.First().Claims.First(c => c.Type == "sub")?.Value;
            idList.Add(userId);

            return idList;
        }

        private async Task<List<string>> GetTeamIdList(Guid teamId, bool checkAccess)
        {
            var idList = new List<string>();
            // add the user's ID
            var userId = Guid.Parse(Context.User.Identities.First().Claims.First(c => c.Type == "sub").Value);
            idList.Add(userId.ToString());

            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<CiteContext>();
            var team = await context.Teams.SingleOrDefaultAsync(t => t.Id == teamId);
            if (team == null || team.EvaluationId == null)
                return idList;

            var evaluationId = team.EvaluationId.Value;
            var evaluation = await context.Evaluations.SingleAsync(e => e.Id == evaluationId);
            var teamGroups = new[] { teamId.ToString(), evaluationId.ToString(), evaluation.ScoringModelId.ToString() };
            var officialGroups = new[] { evaluationId.ToString() + PAST_OFFICIAL_SCORE_POSTFIX, evaluationId.ToString() + OFFICIAL_SCORE_POSTFIX };
            var allMovesGroup = evaluationId.ToString() + ALL_MOVES_POSTFIX;
            if (!checkAccess)
            {
                idList.AddRange(teamGroups);
                idList.AddRange(officialGroups);
                idList.Add(allMovesGroup);
                return idList;
            }

            // moves ahead of the current one follow the same permissions as MoveController's reads
            if (await _authorizationService.AuthorizeAsync<Evaluation>(evaluationId, [SystemPermission.ViewEvaluations, SystemPermission.ObserveEvaluations, SystemPermission.ExecuteEvaluations], [EvaluationPermission.ObserveEvaluation, EvaluationPermission.ViewEvaluation, EvaluationPermission.ExecuteEvaluation], _ct))
            {
                idList.Add(allMovesGroup);
            }

            // an observer of the evaluation can follow any of its teams
            var isObserver = await _authorizationService.AuthorizeAsync<Evaluation>(evaluationId, [SystemPermission.ObserveEvaluations], [EvaluationPermission.ObserveEvaluation], _ct);
            var isMember = await context.TeamMemberships.AnyAsync(m => m.TeamId == teamId && m.UserId == userId);
            if (isObserver || (isMember && await _authorizationService.AuthorizeAsync<Team>(teamId, [], [TeamPermission.ViewTeam], _ct)))
            {
                idList.AddRange(teamGroups);
            }
            else
            {
                return idList;
            }

            // official scores follow the same permissions as SubmissionService.HasSpecificPermission
            if (isObserver || await _authorizationService.AuthorizeAnyTeamAsync(evaluationId, SubmissionService.OfficialScorePermissions(0, 1, SpecificPermission.View), _ct))
            {
                idList.Add(evaluationId.ToString() + PAST_OFFICIAL_SCORE_POSTFIX);
            }
            if (isObserver || await _authorizationService.AuthorizeAnyTeamAsync(evaluationId, SubmissionService.OfficialScorePermissions(0, 0, SpecificPermission.View), _ct))
            {
                idList.Add(evaluationId.ToString() + OFFICIAL_SCORE_POSTFIX);
            }

            return idList;
        }

        private async Task<List<string>> GetAdminIdList()
        {
            var idList = new List<string>();
            var userId = Context.User.Identities.First().Claims.First(c => c.Type == "sub")?.Value;
            idList.Add(userId);
            // content developer or system admin
            if (await _authorizationService.AuthorizeAsync([SystemPermission.ViewEvaluations], new CancellationToken()))
            {
                idList.Add(ADMIN_DATA_GROUP);
            }

            return idList;
        }

    }

    public static class MainHubMethods
    {
        public const string ActionCreated = "ActionCreated";
        public const string ActionUpdated = "ActionUpdated";
        public const string ActionDeleted = "ActionDeleted";
        public const string EvaluationCreated = "EvaluationCreated";
        public const string EvaluationUpdated = "EvaluationUpdated";
        public const string EvaluationDeleted = "EvaluationDeleted";
        public const string MoveCreated = "MoveCreated";
        public const string MoveUpdated = "MoveUpdated";
        public const string MoveDeleted = "MoveDeleted";
        public const string RoleCreated = "RoleCreated";
        public const string RoleUpdated = "RoleUpdated";
        public const string RoleDeleted = "RoleDeleted";
        public const string ScoringCategoryCreated = "ScoringCategoryCreated";
        public const string ScoringCategoryUpdated = "ScoringCategoryUpdated";
        public const string ScoringCategoryDeleted = "ScoringCategoryDeleted";
        public const string ScoringModelCreated = "ScoringModelCreated";
        public const string ScoringModelUpdated = "ScoringModelUpdated";
        public const string ScoringModelDeleted = "ScoringModelDeleted";
        public const string ScoringOptionCreated = "ScoringOptionCreated";
        public const string ScoringOptionUpdated = "ScoringOptionUpdated";
        public const string ScoringOptionDeleted = "ScoringOptionDeleted";
        public const string SubmissionCreated = "SubmissionCreated";
        public const string SubmissionUpdated = "SubmissionUpdated";
        public const string SubmissionDeleted = "SubmissionDeleted";
        public const string TeamCreated = "TeamCreated";
        public const string TeamUpdated = "TeamUpdated";
        public const string TeamDeleted = "TeamDeleted";
        public const string TeamMembershipCreated = "TeamMembershipCreated";
        public const string TeamMembershipUpdated = "TeamMembershipUpdated";
        public const string TeamMembershipDeleted = "TeamMembershipDeleted";
        public const string UserCreated = "UserCreated";
        public const string UserUpdated = "UserUpdated";
        public const string UserDeleted = "UserDeleted";
        public const string GroupMembershipCreated = "GroupMembershipCreated";
        public const string GroupMembershipUpdated = "GroupMembershipUpdated";
        public const string GroupMembershipDeleted = "GroupMembershipDeleted";
        public const string ScoringModelMembershipCreated = "ScoringModelMembershipCreated";
        public const string ScoringModelMembershipUpdated = "ScoringModelMembershipUpdated";
        public const string ScoringModelMembershipDeleted = "ScoringModelMembershipDeleted";
        public const string EvaluationMembershipCreated = "EvaluationMembershipCreated";
        public const string EvaluationMembershipUpdated = "EvaluationMembershipUpdated";
        public const string EvaluationMembershipDeleted = "EvaluationMembershipDeleted";
        public const string ScoringModelCopyProgress = "ScoringModelCopyProgress";
    }
}
