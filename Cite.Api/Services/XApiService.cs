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
using Microsoft.Extensions.Logging;
using Cite.Api.Data;
using Cite.Api.Infrastructure.Extensions;
using Cite.Api.Infrastructure.Authorization;
using Cite.Api.Infrastructure.Exceptions;
using Cite.Api.Infrastructure.Options;
using TinCan;

namespace Cite.Api.Services
{
    public interface IXApiService
    {
        Boolean IsConfigured();
        Task<Boolean> CreateAsync(
            Uri verb,
            Dictionary<String,String> activityData,
            Dictionary<String,String> categoryData,
            Dictionary<String,String> groupingData,
            Dictionary<String,String> parentData,
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
        private readonly RemoteLRS _lrs;
        private readonly Agent _agent;
        private readonly AgentAccount _account;
        private readonly Context _xApiContext;
        private readonly ILogger<XApiService> _logger;

        public XApiService(
            CiteContext context,
            IPrincipal user,
            IAuthorizationService authorizationService,
            XApiOptions xApiOptions,
            ILogger<XApiService> logger)
        {
            _context = context;
            _user = user as ClaimsPrincipal;
            _authorizationService = authorizationService;
            _xApiOptions = xApiOptions;
            _logger = logger;

            if (IsConfigured()) {
                // configure LRS
                _lrs = new TinCan.RemoteLRS(_xApiOptions.Endpoint, _xApiOptions.Username, _xApiOptions.Password);

                // configure AgentAccount
                _account = new TinCan.AgentAccount();
                _account.name = _user.Identities.First().Claims.First(c => c.Type == "sub")?.Value;
                var iss = _user.Identities.First().Claims.First(c => c.Type == "iss")?.Value;
                if (_xApiOptions.IssuerUrl != "") {
                    _account.homePage = new Uri(_xApiOptions.IssuerUrl);
                } else if (iss.Contains("http")) {
                    _account.homePage = new Uri(iss);
                } else if (_xApiOptions.IssuerUrl == "") {
                    _account.homePage = new Uri("http://" + iss);
                }

                // configure Agent
                _agent = new TinCan.Agent();
                _agent.name = _context.Users.Find(_user.GetId()).Name;
                _agent.account = _account;

                // Initialize the Context
                _xApiContext = new Context();
                _xApiContext.platform = _xApiOptions.Platform;
                _xApiContext.language = "en-US";

            }
        }

        public Boolean IsConfigured()
        {
            return _xApiOptions.Username.Length > 0;
        }

        public async Task<Boolean> CreateAsync(
            Uri verbUri, Dictionary<String,String> activityData,
            Dictionary<String,String> parentData,
            Dictionary<String,String> categoryData,
            Dictionary<String,String> groupingData,
            Dictionary<String,String> otherData,
            Guid teamId,
            CancellationToken ct)
        {
            if (!IsConfigured())
            {
                return false;
            };

            if (!(await _authorizationService.AuthorizeAsync(_user, null, new BaseUserRequirement())).Succeeded)
                throw new ForbiddenException();

            var verb = new Verb();
            verb.id = verbUri;
            verb.display = new LanguageMap();
            verb.display.Add("en-US", verb.id.Segments.Last());

            var activity = new Activity();
            activity.id = _xApiOptions.ApiUrl + activityData["type"] + "/" + activityData["id"];
            activity.definition = new TinCan.ActivityDefinition();
            activity.definition.type = new Uri(activityData["activityType"]);
            if (activityData.ContainsKey("moreInfo")) {
                activity.definition.moreInfo = new Uri(_xApiOptions.UiUrl + activityData["moreInfo"]);
            }
            activity.definition.name = new LanguageMap();
            activity.definition.name.Add("en-US", activityData["name"]);
            activity.definition.description = new LanguageMap();
            activity.definition.description.Add("en-US", activityData["description"]);

            var context = new Context();
            context.platform = _xApiContext.platform;
            context.language = _xApiContext.language;

            if (teamId.ToString() !=  "") {
                var team = _context.Teams.Find(teamId);
                var group = new TinCan.Group();
                group.name = team.ShortName;
                if (_xApiOptions.EmailDomain != "") {
                    // this is being set but not logged inside the lrs
                    group.mbox = "mailto:" + team.ShortName + "@" + _xApiOptions.EmailDomain;
                }
                group.account = new AgentAccount();
                group.account.homePage = new Uri(_xApiOptions.UiUrl);
                group.account.name = team.Id.ToString();;
                group.member = new List<Agent> {};
                group.member.Add(_agent);
                context.team = group;
            }

            var contextActivities = new ContextActivities();
            context.contextActivities = contextActivities;

            if (parentData.Count() > 0) {
                var parent = new Activity();
                parent.id = _xApiOptions.ApiUrl + parentData["type"] + "/" + parentData["id"];
                parent.definition = new ActivityDefinition();
                parent.definition.name = new LanguageMap();
                parent.definition.name.Add("en-US", parentData["name"]);
                parent.definition.description = new LanguageMap();
                parent.definition.description.Add("en-US", parentData["description"]);
                parent.definition.type = new Uri(parentData["activityType"]);
                if (parentData.ContainsKey("moreInfo")) {
                    parent.definition.moreInfo = new Uri(_xApiOptions.UiUrl + parentData["moreInfo"]);
                }
                contextActivities.parent = new List<Activity>();
                contextActivities.parent.Add(parent);
            }

            if (otherData.Count() > 0) {
                var other = new TinCan.Activity();
                other.id = _xApiOptions.ApiUrl  + otherData["type"] + "/" + otherData["id"];
                other.definition = new ActivityDefinition();
                other.definition.name = new LanguageMap();
                other.definition.name.Add("en-US", otherData["name"]);
                other.definition.description = new LanguageMap();
                other.definition.description.Add("en-US", otherData["description"]);
                other.definition.type = new Uri(otherData["activityType"]);
                if (otherData.ContainsKey("moreInfo")) {
                    other.definition.moreInfo = new Uri(_xApiOptions.UiUrl + otherData["moreInfo"]);
                }
                contextActivities.other = new List<Activity>();
                context.contextActivities.other.Add(other);
            }
            //var extensions = new Extensions();
            //context.extensions = new TinCan.Extensions();

            if (groupingData.Count() > 0) {
                var grouping = new TinCan.Activity();
                grouping.id = _xApiOptions.ApiUrl  + groupingData["type"] + "/" + groupingData["id"];
                grouping.definition = new ActivityDefinition();
                grouping.definition.name = new LanguageMap();
                grouping.definition.name.Add("en-US", groupingData["name"]);
                grouping.definition.description = new LanguageMap();
                grouping.definition.description.Add("en-US", groupingData["description"]);
                grouping.definition.type = new Uri(groupingData["activityType"]);
                if (groupingData.ContainsKey("moreInfo")) {
                    grouping.definition.moreInfo = new Uri(_xApiOptions.UiUrl + groupingData["moreInfo"]);
                }
                contextActivities.grouping = new List<Activity>();
                context.contextActivities.grouping.Add(grouping);
            }

            if (categoryData.Count() > 0) {
                var category = new TinCan.Activity();
                category.id = _xApiOptions.ApiUrl  + categoryData["type"] + "/" + categoryData["id"];
                category.definition = new ActivityDefinition();
                category.definition.name = new LanguageMap();
                category.definition.name.Add("en-US", categoryData["name"]);
                category.definition.description = new LanguageMap();
                category.definition.description.Add("en-US", categoryData["description"]);
                category.definition.type = new Uri(categoryData["activityType"]);
                if (categoryData.ContainsKey("moreInfo")) {
                    category.definition.moreInfo = new Uri(_xApiOptions.UiUrl + categoryData["moreInfo"]);
                }
                contextActivities.category = new List<Activity>();
                context.contextActivities.category.Add(category);
            }

            var statement = new Statement();
            statement.actor = _agent;
            statement.verb = verb;
            statement.target = activity;
            statement.context = context;

            // TODO pass in separately
            if (activityData.ContainsKey("result"))
            {
                var result = new Result();
                result.response = activityData["result"];
                statement.result = result;
            }

            TinCan.LRSResponses.StatementLRSResponse lrsStatementResponse = _lrs.SaveStatement(statement);
            if (lrsStatementResponse.success)
            {
                // List of statements available
                Console.WriteLine("LRS saved statement from xAPI Service");
            } else {
                Console.WriteLine("ERROR FROM LRS VIA XAPI SERVICE: " + lrsStatementResponse.errMsg);
                return false;
            }

            return true;
        }


    }
}

