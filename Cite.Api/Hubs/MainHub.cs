// Copyright 2022 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license, please see LICENSE.md in the project root for license information or contact permission@sei.cmu.edu for full terms.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
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
        private readonly CiteContext _context;
        private readonly DatabaseOptions _options;
        private readonly CancellationToken _ct;
        private readonly ICiteAuthorizationService _authorizationService;
        public const string ADMIN_DATA_GROUP = "AdminDataGroup";
        public const string EVALUATION_GROUP = "AdminEvaluationGroup";
        public const string SCORING_MODEL_GROUP = "AdminScoringModelGroup";
        public const string GROUP_GROUP = "AdminGroupGroup";
        public const string ROLE_GROUP = "AdminRoleGroup";
        public const string USER_GROUP = "AdminUserGroup";

        public MainHub(
            ITeamService teamService,
            IEvaluationService evaluationService,
            IScoringModelService scoringModelService,
            CiteContext context,
            DatabaseOptions options,
            ICiteAuthorizationService authorizationService
        )
        {
            _teamService = teamService;
            _evaluationService = evaluationService;
            _scoringModelService = scoringModelService;
            _context = context;
            _options = options;
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
                // leave the old team
                var idList = await GetTeamIdList(oldTeamId);
                foreach (var id in idList)
                {
                    await Groups.RemoveFromGroupAsync(Context.ConnectionId, id.ToString());
                }
                // join the new team
                idList = await GetTeamIdList(newTeamId);
                foreach (var id in idList)
                {
                    await Groups.AddToGroupAsync(Context.ConnectionId, id.ToString());
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

        private async Task<List<string>> GetTeamIdList(Guid teamId)
        {
            var idList = new List<string>();
            // add the user's ID
            var userId = Context.User.Identities.First().Claims.First(c => c.Type == "sub")?.Value;
            idList.Add(userId);
            // make sure that the user has access to the requested team
            var team = await _context.Teams.Include(t => t.TeamType).SingleOrDefaultAsync(t => t.Id == teamId);
            var isObserver = await _context.EvaluationMemberships
                .Where(er =>
                    er.EvaluationId == team.EvaluationId &&
                    er.UserId == Guid.Parse(userId) &&
                    (er.Role.AllPermissions || er.Role.Permissions.Contains(EvaluationPermission.ObserveEvaluation)))
                .AnyAsync();
            if (team != null)
            {
                var teamMembership = await _context.TeamMemberships.SingleOrDefaultAsync(tu => tu.Team.EvaluationId == team.EvaluationId && tu.UserId.ToString() == userId);
                if (teamMembership != null && (teamMembership.TeamId == teamId || isObserver))
                {
                    idList.Add(teamId.ToString());
                    idList.Add(team.EvaluationId.ToString());
                    var scoringModelId = (await _context.Evaluations.SingleOrDefaultAsync(e => e.Id == team.EvaluationId)).ScoringModelId;
                    idList.Add(scoringModelId.ToString());
                }
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
    }
}
