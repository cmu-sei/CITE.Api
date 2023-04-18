// Copyright 2022 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license, please see LICENSE.md in the project root for license information or contact permission@sei.cmu.edu for full terms.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Cite.Api.Data;
using Cite.Api.Data.Models;
using Cite.Api.Infrastructure.Extensions;
using Cite.Api.Infrastructure.Authorization;
using Cite.Api.Infrastructure.Exceptions;
using Cite.Api.Infrastructure.Options;
using Cite.Api.ViewModels;
using TinCan;

namespace Cite.Api.Services
{
    public interface IXApiService
    {
        Task<Boolean> CreateAsync(String verb, String description, Guid evaluationId, Guid team, CancellationToken ct);
    }

    public class XApiService : IXApiService
    {
        private readonly CiteContext _context;
        private readonly ClaimsPrincipal _user;
        private readonly IAuthorizationService _authorizationService;
        private readonly IUserClaimsService _userClaimsService;
        private readonly IMapper _mapper;
        private readonly XApiOptions _xApiOptions;
        private readonly RemoteLRS _lrs;
        private readonly Verb _verb;
        private readonly AgentAccount _account;
        private readonly Activity _activity;
        private readonly Agent _agent;
        private readonly Statement _statement;
        private readonly Context _xApiContext;
        public XApiService(CiteContext context, IPrincipal user, IAuthorizationService authorizationService, IUserClaimsService userClaimsService, IMapper mapper, XApiOptions xApiOptions)
        {
            _context = context;
            _user = user as ClaimsPrincipal;
            _authorizationService = authorizationService;
            _userClaimsService = userClaimsService;
            _mapper = mapper;
            _xApiOptions = xApiOptions;

            // configure LRS
            _lrs = new TinCan.RemoteLRS(_xApiOptions.Endpoint, _xApiOptions.Username, _xApiOptions.Password);

            // configure Agent
            _account = new TinCan.AgentAccount();
            _account.homePage = new Uri(_xApiOptions.HomePage);
            _agent = new TinCan.Agent();

            // Initalilze Verb and Activity
            _verb = new TinCan.Verb();
            _verb.display = new LanguageMap();
            _activity = new TinCan.Activity();

            // Initialize the Context
            _xApiContext = new Context();
            _xApiContext.platform = "CITE";
            _xApiContext.language = "en-US";

            // Initalize Statement
            _statement = new TinCan.Statement();

        }


        public async Task<Boolean> CreateAsync(String verb, String description, Guid evaluationId, Guid teamId, CancellationToken ct)
        {
            if (!(await _authorizationService.AuthorizeAsync(_user, null, new FullRightsRequirement())).Succeeded)
                throw new ForbiddenException();

            var user = _context.Users.Find(_user.GetId());
            _account.name = user.Name;
            _account.homePage = new Uri(_xApiOptions.HomePage);
            _agent.account = _account;

            _verb.id = new Uri ("http://adlnet.gov/expapi/verbs/" + verb);
            _verb.display.Add("en-US", verb);

            _activity.id = "http://localhost:4721/?evaluation=" + evaluationId;
            _activity.definition = new TinCan.ActivityDefinition();
            _activity.definition.type = new Uri("http://adlnet.gov/expapi/activities/simulation");
            _activity.definition.moreInfo = new Uri("http://cite.local");
            _activity.definition.name = new LanguageMap();
            _activity.definition.name.Add("en-US", description);
            _activity.definition.description = new LanguageMap();
            _activity.definition.description.Add("en-US", description);


            if (teamId.ToString() !=  "") {
                var team = _context.Teams.Find(teamId);
                var group = new TinCan.Group();
                group.mbox = "mailto:" + team.ShortName + "@example.com";
                group.name = team.ShortName;

                group.account = new AgentAccount();
                group.account.homePage = new Uri(_xApiOptions.HomePage);
                group.account.name = team.ShortName;
                //group.account.name = teamId.ToString();
                group.member = new List<Agent> {_agent};
                //group.member.Add(_agent);

                _xApiContext.team = group;
            }
            //context.extensions = new TinCan.Extensions();
            //var ext = new TinCan.Extensions();
            //context.extensions.


            _statement.actor = _agent;
            _statement.verb = _verb;
            _statement.target = _activity;
            _statement.context = _xApiContext;

            TinCan.LRSResponses.StatementLRSResponse lrsStatementResponse = _lrs.SaveStatement(_statement);
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

