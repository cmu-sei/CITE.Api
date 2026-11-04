// Copyright 2022 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license, please see LICENSE.md in the project root for license information or contact permission@sei.cmu.edu for full terms.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Cite.Api.Data;
using Cite.Api.Data.Models;
using Cite.Api.Infrastructure.Extensions;
using Cite.Api.Infrastructure.Options;
using TinCan;

namespace Cite.Api.Services
{
    public interface IXApiService
    {
        Boolean IsConfigured();
        Task<Boolean> EvaluationDashboardViewedAsync(Guid evaluationId, CancellationToken ct);
        Task<Boolean> EvaluationScoresheetViewedAsync(Guid evaluationId, CancellationToken ct);
        Task<Boolean> EvaluationDashboardObservedAsync(Guid evaluationId, Guid teamId, CancellationToken ct);
        Task<Boolean> EvaluationScoresheetObservedAsync(Guid evaluationId, Guid teamId, CancellationToken ct);
        Task<Boolean> CreateAsync(
            Uri verb,
            Dictionary<String,String> activityData,
            Dictionary<String,String> parentData,
            Dictionary<String,String> categoryData,
            List<Dictionary<String,String>> groupingData,
            Dictionary<String,String> otherData,
            Guid teamId,
            CancellationToken ct);
    }

    public class XApiService : IXApiService
    {
        private readonly CiteContext _context;
        private readonly ClaimsPrincipal _user;
        private readonly IAuthorizationService _authorizationService;
        private readonly XApiOptions _xApiOptions;
        private readonly IXApiQueueService _queueService;
        private Agent _agent;
        private AgentAccount _account;
        private Context _xApiContext;
        private readonly ILogger<XApiService> _logger;

        public XApiService(
            CiteContext context,
            IPrincipal user,
            IAuthorizationService authorizationService,
            XApiOptions xApiOptions,
            IXApiQueueService queueService,
            ILogger<XApiService> logger)
        {
            _context = context;
            _user = user as ClaimsPrincipal;
            _authorizationService = authorizationService;
            _xApiOptions = xApiOptions;
            _queueService = queueService;
            _logger = logger;
        }

        private void EnsureAgentInitialized()
        {
            if (_agent != null || !IsConfigured())
                return;

            // configure AgentAccount
            _account = new TinCan.AgentAccount();
            _account.Name = _user.Identities.First().Claims.First(c => c.Type == "sub")?.Value;
            var iss = _user.Identities.First().Claims.First(c => c.Type == "iss")?.Value;
            if (_xApiOptions.IssuerUrl != "") {
                _account.HomePage = new Uri(_xApiOptions.IssuerUrl);
            } else if (iss.Contains("http")) {
                _account.HomePage = new Uri(iss);
            } else if (_xApiOptions.IssuerUrl == "") {
                _account.HomePage = new Uri("http://" + iss);
            }

            // configure Agent
            _agent = new TinCan.Agent();
            _agent.Name = _context.Users.Find(_user.GetId()).Name;
            _agent.Account = _account;

            // Initialize the Context
            _xApiContext = new Context();
            _xApiContext.Platform = _xApiOptions.Platform;
            _xApiContext.Language = "en-US";
        }

        public Boolean IsConfigured()
        {
            return _xApiOptions.Enabled && !string.IsNullOrWhiteSpace(_xApiOptions.Username);
        }

        public async Task<Boolean> CreateAsync(
            Uri verbUri, Dictionary<String,String> activityData,
            Dictionary<String,String> parentData,
            Dictionary<String,String> categoryData,
            List<Dictionary<String,String>> groupingData,
            Dictionary<String,String> otherData,
            Guid teamId,
            CancellationToken ct)
        {
            if (!IsConfigured())
            {
                return false;
            };

            // Initialize agent lazily (only when needed, not in constructor)
            EnsureAgentInitialized();

            var verb = new Verb();
            verb.Id = verbUri;
            verb.Display = new LanguageMap();
            verb.Display.Add("en-US", verb.Id.Segments.Last());

            var activity = new Activity();
            activity.Id = _xApiOptions.ApiUrl + activityData["type"] + "/" + activityData["id"];
            activity.Definition = new TinCan.ActivityDefinition();
            activity.Definition.Type = new Uri(activityData["activityType"]);
            if (activityData.ContainsKey("moreInfo")) {
                activity.Definition.MoreInfo = new Uri(_xApiOptions.UiUrl.TrimEnd('/') + activityData["moreInfo"]);
            }
            activity.Definition.Name = new LanguageMap();
            activity.Definition.Name.Add("en-US", activityData["name"]);
            activity.Definition.Description = new LanguageMap();
            activity.Definition.Description.Add("en-US", activityData["description"]);

            var context = new Context();
            context.Platform = _xApiContext.Platform;
            context.Language = _xApiContext.Language;

            // Set registration to evaluation ID if available (groups statements by evaluation session)
            if (parentData.Count() > 0 && parentData.ContainsKey("type") && parentData.ContainsKey("id")) {
                var parentType = parentData["type"].ToLower();
                if (parentType == "evaluation" || parentType == "evaluations") {
                    context.Registration = Guid.Parse(parentData["id"]);
                }
            }

            if (teamId != Guid.Empty) {
                var team = _context.Teams.Find(teamId);
                if (team == null) return false;
                var group = new TinCan.Group();
                group.Name = team.ShortName;
                if (_xApiOptions.EmailDomain != "") {
                    // this is being set but not logged inside the lrs
                    group.Mbox = "mailto:" + team.ShortName + "@" + _xApiOptions.EmailDomain;
                }
                group.Account = new AgentAccount();
                group.Account.HomePage = new Uri(_xApiOptions.UiUrl);
                group.Account.Name = team.Id.ToString();;
                group.Member = new List<Agent> {};
                if (verb.Id.Segments.Last() != "observed") {
                    group.Member.Add(_agent);
                }
                if (otherData.ContainsKey("type") && otherData["type"] == "users") {
                    var targetUser = new Agent();
                    targetUser.Name = otherData["name"];
                    targetUser.Account = new AgentAccount();
                    targetUser.Account.Name = otherData["id"];
                    // set homepage based on current user issuer or default to config file value
                    var iss = _user.Identities.First().Claims.First(c => c.Type == "iss")?.Value;
                    if (_xApiOptions.IssuerUrl != "") {
                        targetUser.Account.HomePage = new Uri(_xApiOptions.IssuerUrl);
                    } else if (iss.Contains("http")) {
                        targetUser.Account.HomePage = new Uri(iss);
                    } else if (_xApiOptions.IssuerUrl == "") {
                        targetUser.Account.HomePage = new Uri("http://" + iss);
                    }
                    group.Member.Add(targetUser);
                }
                context.Team = group;
            }

            var contextActivities = new ContextActivities();
            context.ContextActivities = contextActivities;

            if (parentData.Count() > 0) {
                var parent = new Activity();
                parent.Id = _xApiOptions.ApiUrl + parentData["type"] + "/" + parentData["id"];
                parent.Definition = new ActivityDefinition();
                parent.Definition.Name = new LanguageMap();
                parent.Definition.Name.Add("en-US", parentData["name"]);
                parent.Definition.Description = new LanguageMap();
                parent.Definition.Description.Add("en-US", parentData["description"]);
                parent.Definition.Type = new Uri(parentData["activityType"]);
                if (parentData.ContainsKey("moreInfo")) {
                    parent.Definition.MoreInfo = new Uri(_xApiOptions.UiUrl.TrimEnd('/') + parentData["moreInfo"]);
                }
                contextActivities.Parent = new List<Activity>();
                contextActivities.Parent.Add(parent);
            }

            if (otherData.Count() > 0) {
                var other = new TinCan.Activity();
                other.Id = _xApiOptions.ApiUrl  + otherData["type"] + "/" + otherData["id"];
                other.Definition = new ActivityDefinition();
                other.Definition.Name = new LanguageMap();
                other.Definition.Name.Add("en-US", otherData["name"]);
                other.Definition.Description = new LanguageMap();
                other.Definition.Description.Add("en-US", otherData["description"]);
                other.Definition.Type = new Uri(otherData["activityType"]);
                if (otherData.ContainsKey("moreInfo")) {
                    other.Definition.MoreInfo = new Uri(_xApiOptions.UiUrl.TrimEnd('/') + otherData["moreInfo"]);
                }
                contextActivities.Other = new List<Activity>();
                context.ContextActivities.Other.Add(other);
            }

            if (groupingData != null && groupingData.Count() > 0) {
                contextActivities.Grouping = new List<Activity>();
                foreach (var groupingItem in groupingData) {
                    if (groupingItem.Count() > 0) {
                        var grouping = new TinCan.Activity();
                        grouping.Id = _xApiOptions.ApiUrl  + groupingItem["type"] + "/" + groupingItem["id"];
                        grouping.Definition = new ActivityDefinition();
                        grouping.Definition.Name = new LanguageMap();
                        grouping.Definition.Name.Add("en-US", groupingItem["name"]);
                        grouping.Definition.Description = new LanguageMap();
                        grouping.Definition.Description.Add("en-US", groupingItem["description"]);
                        grouping.Definition.Type = new Uri(groupingItem["activityType"]);
                        if (groupingItem.ContainsKey("moreInfo")) {
                            grouping.Definition.MoreInfo = new Uri(_xApiOptions.UiUrl.TrimEnd('/') + groupingItem["moreInfo"]);
                        }
                        context.ContextActivities.Grouping.Add(grouping);
                    }
                }
            }

            if (categoryData.Count() > 0) {
                var category = new TinCan.Activity();
                category.Id = _xApiOptions.ApiUrl  + categoryData["type"] + "/" + categoryData["id"];
                category.Definition = new ActivityDefinition();
                category.Definition.Name = new LanguageMap();
                category.Definition.Name.Add("en-US", categoryData["name"]);
                category.Definition.Description = new LanguageMap();
                category.Definition.Description.Add("en-US", categoryData["description"]);
                category.Definition.Type = new Uri(categoryData["activityType"]);
                if (categoryData.ContainsKey("moreInfo")) {
                    category.Definition.MoreInfo = new Uri(_xApiOptions.UiUrl.TrimEnd('/') + categoryData["moreInfo"]);
                }
                contextActivities.Category = new List<Activity>();
                context.ContextActivities.Category.Add(category);
            }

            var statement = new Statement();
            statement.Actor = _agent;
            statement.Verb = verb;
            statement.Target = activity;
            statement.Context = context;

            // TODO pass in separately
            if (activityData.ContainsKey("result"))
            {
                var result = new Result();
                result.Response = activityData["result"];
                statement.Result = result;
            }

            // Queue the statement instead of sending directly
            try
            {
                var queuedStatement = new XApiQueuedStatementEntity
                {
                    StatementJson = statement.ToJson(true),
                    Verb = verbUri.ToString(),
                    ActivityId = activity.Id,
                    TeamId = teamId != Guid.Empty ? teamId : null,
                    CreatedBy = _user.GetId()
                };

                await _queueService.EnqueueAsync(queuedStatement, ct);
                _logger.LogInformation("Queued xAPI statement for verb {Verb}", verbUri);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to queue xAPI statement");
                return false;
            }
        }

        public async Task<Boolean> EvaluationDashboardViewedAsync(Guid evaluationId, CancellationToken ct)
        {
            var evaluation = await _context.Evaluations.FindAsync(evaluationId);
            if (evaluation == null) return false;

            var verb = new Uri("http://id.tincanapi.com/verb/viewed");

            var teamUser = await _context.TeamUsers
                .Include(tu => tu.Team)
                .SingleOrDefaultAsync(tu => tu.UserId == _user.GetId() && tu.Team.EvaluationId == evaluationId, ct);
            var teamId = teamUser?.TeamId ?? Guid.Empty;

            var activity = new Dictionary<String,String>();
            activity.Add("id", evaluation.Id.ToString());
            activity.Add("name", "Dashboard");
            activity.Add("description", "The CITE Dashboard shows status indicators for the evaluation.");
            activity.Add("type", "evaluation");
            activity.Add("activityType", "http://id.tincanapi.com/activitytype/resource");
            activity.Add("moreInfo", "?section=dashboard&evaluation=" + evaluation.Id.ToString());

            var parent = new Dictionary<String,String>();
            parent.Add("id", evaluation.Id.ToString());
            parent.Add("name", evaluation.Description);
            parent.Add("description", evaluation.Description);
            parent.Add("type", "evaluation");
            parent.Add("activityType", "http://id.tincanapi.com/activitytype/resource");
            parent.Add("moreInfo", "?evaluation=" + evaluation.Id.ToString());

            var category = new Dictionary<String,String>();
            var grouping = new List<Dictionary<String,String>>();
            var other = new Dictionary<String,String>();

            if (evaluation.CurrentMoveNumber >= 0)
            {
                var moveGrouping = new Dictionary<String,String>();
                moveGrouping.Add("id", evaluation.CurrentMoveNumber.ToString());
                moveGrouping.Add("name", $"Move {evaluation.CurrentMoveNumber}");
                moveGrouping.Add("description", "");
                moveGrouping.Add("type", $"evaluation/{evaluation.Id}/move");
                moveGrouping.Add("activityType", "http://id.tincanapi.com/activitytype/collection-simple");
                moveGrouping.Add("moreInfo", "");
                grouping.Add(moveGrouping);
            }

            return await CreateAsync(
                verb, activity, parent, category, grouping, other, teamId, ct);
        }

        public async Task<Boolean> EvaluationScoresheetViewedAsync(Guid evaluationId, CancellationToken ct)
        {
            var evaluation = await _context.Evaluations.FindAsync(evaluationId);
            if (evaluation == null) return false;

            var verb = new Uri("http://id.tincanapi.com/verb/viewed");

            var teamUser = await _context.TeamUsers
                .Include(tu => tu.Team)
                .SingleOrDefaultAsync(tu => tu.UserId == _user.GetId() && tu.Team.EvaluationId == evaluationId, ct);
            var teamId = teamUser?.TeamId ?? Guid.Empty;

            var activity = new Dictionary<String,String>();
            activity.Add("id", evaluation.Id.ToString());
            activity.Add("name", "Scoresheet");
            activity.Add("description", "The CITE Scoresheet is where teams enter their risk assessment scores.");
            activity.Add("type", "evaluation");
            activity.Add("activityType", "http://id.tincanapi.com/activitytype/resource");
            activity.Add("moreInfo", "?section=scoresheet&evaluation=" + evaluation.Id.ToString());

            var parent = new Dictionary<String,String>();
            parent.Add("id", evaluation.Id.ToString());
            parent.Add("name", evaluation.Description);
            parent.Add("description", evaluation.Description);
            parent.Add("type", "evaluation");
            parent.Add("activityType", "http://id.tincanapi.com/activitytype/resource");
            parent.Add("moreInfo", "?evaluation=" + evaluation.Id.ToString());

            var category = new Dictionary<String,String>();
            var grouping = new List<Dictionary<String,String>>();
            var other = new Dictionary<String,String>();

            if (evaluation.CurrentMoveNumber >= 0)
            {
                var moveGrouping = new Dictionary<String,String>();
                moveGrouping.Add("id", evaluation.CurrentMoveNumber.ToString());
                moveGrouping.Add("name", $"Move {evaluation.CurrentMoveNumber}");
                moveGrouping.Add("description", "");
                moveGrouping.Add("type", $"evaluation/{evaluation.Id}/move");
                moveGrouping.Add("activityType", "http://id.tincanapi.com/activitytype/collection-simple");
                moveGrouping.Add("moreInfo", "");
                grouping.Add(moveGrouping);
            }

            return await CreateAsync(
                verb, activity, parent, category, grouping, other, teamId, ct);
        }

        public async Task<Boolean> EvaluationDashboardObservedAsync(Guid evaluationId, Guid teamId, CancellationToken ct)
        {
            var evaluation = await _context.Evaluations.FindAsync(evaluationId);
            if (evaluation == null) return false;

            var verb = new Uri("https://w3id.org/xapi/dod-isd/verbs/observed");

            var activity = new Dictionary<String,String>();
            activity.Add("id", evaluation.Id.ToString());
            activity.Add("name", "Dashboard");
            activity.Add("description", "The CITE Dashboard shows status indicators for the evaluation.");
            activity.Add("type", "evaluation");
            activity.Add("activityType", "http://id.tincanapi.com/activitytype/resource");
            activity.Add("moreInfo", "?section=dashboard&evaluation=" + evaluation.Id.ToString());

            var parent = new Dictionary<String,String>();
            parent.Add("id", evaluation.Id.ToString());
            parent.Add("name", evaluation.Description);
            parent.Add("description", evaluation.Description);
            parent.Add("type", "evaluation");
            parent.Add("activityType", "http://id.tincanapi.com/activitytype/resource");
            parent.Add("moreInfo", "?evaluation=" + evaluation.Id.ToString());

            var category = new Dictionary<String,String>();
            var grouping = new List<Dictionary<String,String>>();
            var other = new Dictionary<String,String>();

            if (evaluation.CurrentMoveNumber >= 0)
            {
                var moveGrouping = new Dictionary<String,String>();
                moveGrouping.Add("id", evaluation.CurrentMoveNumber.ToString());
                moveGrouping.Add("name", $"Move {evaluation.CurrentMoveNumber}");
                moveGrouping.Add("description", "");
                moveGrouping.Add("type", $"evaluation/{evaluation.Id}/move");
                moveGrouping.Add("activityType", "http://id.tincanapi.com/activitytype/collection-simple");
                moveGrouping.Add("moreInfo", "");
                grouping.Add(moveGrouping);
            }

            return await CreateAsync(
                verb, activity, parent, category, grouping, other, teamId, ct);
        }

        public async Task<Boolean> EvaluationScoresheetObservedAsync(Guid evaluationId, Guid teamId, CancellationToken ct)
        {
            var evaluation = await _context.Evaluations.FindAsync(evaluationId);
            if (evaluation == null) return false;

            var verb = new Uri("https://w3id.org/xapi/dod-isd/verbs/observed");

            var activity = new Dictionary<String,String>();
            activity.Add("id", evaluation.Id.ToString());
            activity.Add("name", "Scoresheet");
            activity.Add("description", "The CITE Scoresheet is where teams enter their risk assessment scores.");
            activity.Add("type", "evaluation");
            activity.Add("activityType", "http://id.tincanapi.com/activitytype/resource");
            activity.Add("moreInfo", "?section=scoresheet&evaluation=" + evaluation.Id.ToString());

            var parent = new Dictionary<String,String>();
            parent.Add("id", evaluation.Id.ToString());
            parent.Add("name", evaluation.Description);
            parent.Add("description", evaluation.Description);
            parent.Add("type", "evaluation");
            parent.Add("activityType", "http://id.tincanapi.com/activitytype/resource");
            parent.Add("moreInfo", "?evaluation=" + evaluation.Id.ToString());

            var category = new Dictionary<String,String>();
            var grouping = new List<Dictionary<String,String>>();
            var other = new Dictionary<String,String>();

            if (evaluation.CurrentMoveNumber >= 0)
            {
                var moveGrouping = new Dictionary<String,String>();
                moveGrouping.Add("id", evaluation.CurrentMoveNumber.ToString());
                moveGrouping.Add("name", $"Move {evaluation.CurrentMoveNumber}");
                moveGrouping.Add("description", "");
                moveGrouping.Add("type", $"evaluation/{evaluation.Id}/move");
                moveGrouping.Add("activityType", "http://id.tincanapi.com/activitytype/collection-simple");
                moveGrouping.Add("moreInfo", "");
                grouping.Add(moveGrouping);
            }

            return await CreateAsync(
                verb, activity, parent, category, grouping, other, teamId, ct);
        }


    }
}
