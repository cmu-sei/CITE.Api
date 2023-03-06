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
using Cite.Api.Services;
using Cite.Api.Infrastructure.Authorization;
using Cite.Api.Infrastructure.Options;

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
        private readonly IAuthorizationService _authorizationService;
        public const string ADMIN_DATA_GROUP = "AdminDataGroup";

        public MainHub(
            ITeamService teamService,
            IEvaluationService evaluationService,
            IScoringModelService scoringModelService,
            CiteContext context,
            DatabaseOptions options,
            IAuthorizationService authorizationService
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
            var idList = new List<string>();
            var userId = Context.User.Identities.First().Claims.First(c => c.Type == "sub")?.Value;
            idList.Add(userId);
            // user's teams
            var teamList = await  _context.TeamUsers
                .Where(tu => tu.UserId == Guid.Parse(userId))
                .Include(tu => tu.Team)
                .ThenInclude(t => t.TeamType)
                .Select(tu => tu.Team)
                .ToListAsync();
            var teamIdList = teamList.Select(t => t.Id.ToString()).ToList();
            idList.AddRange(teamIdList);
            // user's evaluations
            var evaluationTeamList = await _context.Teams
                .Where(t => teamIdList.Contains(t.Id.ToString()) && t.Evaluation.Status == Data.Enumerations.ItemStatus.Active)
                .Include(t => t.Evaluation)
                .ToListAsync();
            var evaluationIdList = evaluationTeamList
                .Where(t => teamIdList.Contains(t.Id.ToString()) && t.Evaluation.Status == Data.Enumerations.ItemStatus.Active)
                .Select(et => et.Evaluation.Id.ToString())
                .ToList();
            idList.AddRange(evaluationIdList);
            // user's official contributor evaluations
            var officialContributorTeamIdList = teamList.Where(t => t.TeamType.Name == _options.OfficialScoreTeamTypeName).Select(t => t.Id.ToString()).ToList();
            evaluationIdList = evaluationTeamList
                .Where(t => officialContributorTeamIdList.Contains(t.Id.ToString()) && t.Evaluation.Status == Data.Enumerations.ItemStatus.Active)
                .Select(t => t.Evaluation.Id.ToString() + _options.OfficialScoreTeamTypeName.Replace(" ", ""))
                .ToList();
            idList.AddRange(evaluationIdList);
            // user's scoring models
            var scoringModelIdList = evaluationTeamList.Select(t => t.Evaluation.ScoringModelId.ToString()).Distinct().ToList();
            idList.AddRange(scoringModelIdList);

            return idList;
        }

        private async Task<List<string>> GetAdminIdList()
        {
            var idList = new List<string>();
            var userId = Context.User.Identities.First().Claims.First(c => c.Type == "sub")?.Value;
            idList.Add(userId);
            // content developer or system admin
            if ((await _authorizationService.AuthorizeAsync(Context.User, null, new ContentDeveloperRequirement())).Succeeded)
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
        public const string EvaluationTeamCreated = "EvaluationTeamCreated";
        public const string EvaluationTeamUpdated = "EvaluationTeamUpdated";
        public const string EvaluationTeamDeleted = "EvaluationTeamDeleted";
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
        public const string TeamUserCreated = "TeamUserCreated";
        public const string TeamUserUpdated = "TeamUserUpdated";
        public const string TeamUserDeleted = "TeamUserDeleted";
        public const string UserCreated = "UserCreated";
        public const string UserUpdated = "UserUpdated";
        public const string UserDeleted = "UserDeleted";
        public const string UserPermissionCreated = "UserPermissionCreated";
        public const string UserPermissionDeleted = "UserPermissionDeleted";
    }
}
