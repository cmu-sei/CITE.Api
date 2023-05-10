// Copyright 2022 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license, please see LICENSE.md in the project root for license information or contact permission@sei.cmu.edu for full terms.

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using MediatR;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Cite.Api.Data;
using Cite.Api.Data.Models;
using Cite.Api.Hubs;
using Cite.Api.Infrastructure.Authorization;
using Cite.Api.Infrastructure.Extensions;
using Cite.Api.Infrastructure.Options;
using Cite.Api.Services;
using TinCan;
using System;

namespace Cite.Api.Infrastructure.EventHandlers
{
    public class BaseSubmissionHandler
    {
        protected readonly CiteContext _db;
        protected readonly IMapper _mapper;
        protected readonly ISubmissionService _submissionService;
        protected readonly IHubContext<MainHub> _mainHub;
        private readonly XApiOptions _xApiOptions;
        private readonly DatabaseOptions _options;

        public BaseSubmissionHandler(
            CiteContext db,
            IMapper mapper,
            ISubmissionService submissionService,
            IHubContext<MainHub> mainHub,
            XApiOptions xApiOptions,
            DatabaseOptions options)
        {
            _db = db;
            _mapper = mapper;
            _submissionService = submissionService;
            _mainHub = mainHub;
            _options = options;
            _xApiOptions = xApiOptions;
        }

        protected async Task<string[]> GetSignalrGroupsForSubmissionAsync(SubmissionEntity submissionEntity, CancellationToken cancellationToken)
        {
            var signalrGroupIds = new List<string>();

            // add this submission ID to the signalr notify list
            signalrGroupIds.Add(submissionEntity.Id.ToString());
            // if this is a user's submission, add the user ID to the signalr notify list
            if (submissionEntity.UserId != null)
            {
                signalrGroupIds.Add(submissionEntity.UserId.ToString());
            }
            // if this is a team's submission, add the team ID to the signalr notify list
            else if (submissionEntity.TeamId != null)
            {
                signalrGroupIds.Add(submissionEntity.TeamId.ToString());
            }
            // if this is an evaluation's submission and the move number is not the current one, add the evaluation ID to the signalr notify list
            else if (submissionEntity.EvaluationId != null)
            {
                var currentMoveNumber = (await _db.Evaluations.FindAsync(submissionEntity.EvaluationId)).CurrentMoveNumber;
                if (submissionEntity.MoveNumber < currentMoveNumber)
                {
                    signalrGroupIds.Add(submissionEntity.EvaluationId.ToString());
                }
                // also send to evaluation official score contributors
                signalrGroupIds.Add(submissionEntity.EvaluationId.ToString() + "OfficialScore");
            }
            // the admin data group gets everything
            signalrGroupIds.Add(MainHub.ADMIN_DATA_GROUP);

            return signalrGroupIds.ToArray();
        }

        protected async Task HandleCreateOrUpdate(
            SubmissionEntity submissionEntity,
            string method,
            string[] modifiedProperties,
            CancellationToken cancellationToken)
        {
            var signalrGroupIds = await this.GetSignalrGroupsForSubmissionAsync(submissionEntity, cancellationToken);
            var fullEntity = await _db.Submissions
                .Include(sm => sm.SubmissionCategories)
                .ThenInclude(sc => sc.SubmissionOptions)
                .ThenInclude(so => so.SubmissionComments)
                .SingleAsync(sm => sm.Id == submissionEntity.Id);

            var submission = _mapper.Map<ViewModels.Submission>(fullEntity);
            var tasks = new List<Task>();

            foreach (var signalrGroupId in signalrGroupIds)
            {
                tasks.Add(_mainHub.Clients.Group(signalrGroupId.ToString()).SendAsync(method, submission, modifiedProperties, cancellationToken));
            }
            var averageTasks = await GetAverageSubmissionTasks(submissionEntity, method, modifiedProperties, cancellationToken);
            tasks.AddRange(averageTasks);

            await Task.WhenAll(tasks);


            var host = _xApiOptions.Endpoint;
            var username = _xApiOptions.Username;
            var password = _xApiOptions.Password;
            var lrs = new TinCan.RemoteLRS(host, username, password);
            //var guid = submissionEntity.UserId;

            var account = new TinCan.AgentAccount();
            account.name = (await _db.Users.SingleAsync(s => s.Id == submissionEntity.UserId)).Name;
            account.homePage = new Uri(_xApiOptions.HomePage);
            var agent = new TinCan.Agent();
            agent.account = account;

            var verb = new TinCan.Verb();
            verb.display = new LanguageMap();
            if (method == "SubmissionCreated") {
                // initialized or opened or launched
                verb.id = new Uri ("http://adlnet.gov/expapi/verbs/initialized");
                verb.display.Add("en-US", "initialized");
            } else if (method == "SubmissionUpdated") {
                // it is also possible to check modifiedProperties
                // comments do not show up in this handler
                // interacted, answered, attempted, progressed, commented, selected
                verb.id = new Uri ("http://adlnet.gov/expapi/verbs/interacted");
                verb.display.Add("en-US", "interacted");
            } else {
                verb.id = new Uri ("http://adlnet.gov/expapi/verbs/experienced");
                verb.display.Add("en-US", "experienced");
            }
            var activity = new TinCan.Activity();
            activity.id = "http://localhost:4721/?evaluation" + submissionEntity.EvaluationId.ToString();
            var  definition = new TinCan.ActivityDefinition();
            definition.type = new Uri("http://adlnet.gov/expapi/activities/simulation");
            definition.moreInfo = new Uri("http://cite.local");
            definition.description = new LanguageMap();
            definition.description.Add("en-US", method);
            definition.name = new LanguageMap();
            definition.name.Add("en-US", method);
            activity.definition = definition;

            var statement = new TinCan.Statement();
            statement.actor = agent;
            statement.verb = verb;
            statement.target = activity;
            TinCan.LRSResponses.StatementLRSResponse lrsStatementResponse = lrs.SaveStatement(statement);
            if (lrsStatementResponse.success)
            {
                // List of statements available
                Console.WriteLine("LRS saved statment");
            } else {
                Console.WriteLine("ERROR FROM LRS: " + lrsStatementResponse.errMsg);

            }



        }

        private async Task<IEnumerable<Task>> GetAverageSubmissionTasks(
            SubmissionEntity submission,
            string method,
            string[] modifiedProperties,
            CancellationToken cancellationToken)
        {
            var tasks = new List<Task>();
            // create the task to send the team average
            if (submission.UserId != null)
            {
                var averageSubmission = await _submissionService.GetTeamAverageAsync(submission, cancellationToken);
                if (averageSubmission != null)
                {
                    tasks.Add(_mainHub.Clients.Group(submission.TeamId.ToString()).SendAsync(method, averageSubmission, modifiedProperties, cancellationToken));
                }
            }
            else if (submission.TeamId != null)
            {
                var teamType = await _db.Teams.Select(t => t.TeamType).SingleOrDefaultAsync(t => t.Id == submission.TeamId);
                if (teamType != null && teamType.ShowTeamTypeAverage)
                {
                    // create the task to send the teamType average
                    var averageSubmission = await _submissionService.GetTypeAverageAsync(_mapper.Map<ViewModels.Submission>(submission), cancellationToken);
                    if (averageSubmission != null)
                    {
                        tasks.Add(_mainHub.Clients.Group(averageSubmission.EvaluationId.ToString() + teamType.Id).SendAsync(method, averageSubmission, modifiedProperties, cancellationToken));
                    }
                }
            }

            return tasks;
        }
    }

    public class SubmissionCreatedSignalRHandler : BaseSubmissionHandler, INotificationHandler<EntityCreated<SubmissionEntity>>
    {
        public SubmissionCreatedSignalRHandler(
            CiteContext db,
            IMapper mapper,
            ISubmissionService submissionService,
            IHubContext<MainHub> mainHub,
            XApiOptions xApiOptions,
            DatabaseOptions options) : base(db, mapper, submissionService, mainHub, xApiOptions, options)
            {
            }

        public async Task Handle(EntityCreated<SubmissionEntity> notification, CancellationToken cancellationToken)
        {
            await base.HandleCreateOrUpdate(notification.Entity, MainHubMethods.SubmissionCreated, null, cancellationToken);
        }
    }

    public class SubmissionUpdatedSignalRHandler : BaseSubmissionHandler, INotificationHandler<EntityUpdated<SubmissionEntity>>
    {
        public SubmissionUpdatedSignalRHandler(
            CiteContext db,
            IMapper mapper,
            ISubmissionService submissionService,
            IHubContext<MainHub> mainHub,
            XApiOptions xApiOptions,
            DatabaseOptions options) : base(db, mapper, submissionService, mainHub, xApiOptions, options)
            {
            }

        public async Task Handle(EntityUpdated<SubmissionEntity> notification, CancellationToken cancellationToken)
        {
            await base.HandleCreateOrUpdate(
                notification.Entity,
                MainHubMethods.SubmissionUpdated,
                notification.ModifiedProperties.Select(x => x.TitleCaseToCamelCase()).ToArray(),
                cancellationToken);
        }
    }

    public class SubmissionDeletedSignalRHandler : BaseSubmissionHandler, INotificationHandler<EntityDeleted<SubmissionEntity>>
    {
        public SubmissionDeletedSignalRHandler(
            CiteContext db,
            IMapper mapper,
            ISubmissionService submissionService,
            IHubContext<MainHub> mainHub,
            XApiOptions xApiOptions,
            DatabaseOptions options) : base(db, mapper, submissionService, mainHub, xApiOptions, options)
        {
        }

        public async Task Handle(EntityDeleted<SubmissionEntity> notification, CancellationToken cancellationToken)
        {
            var signalrGroupIds = await base.GetSignalrGroupsForSubmissionAsync(notification.Entity, cancellationToken);
            var tasks = new List<Task>();

            foreach (var signalrGroupId in signalrGroupIds)
            {
                tasks.Add(_mainHub.Clients.Group(signalrGroupId.ToString()).SendAsync(MainHubMethods.SubmissionDeleted, notification.Entity.Id, cancellationToken));
            }

            await Task.WhenAll(tasks);
        }
    }
}
