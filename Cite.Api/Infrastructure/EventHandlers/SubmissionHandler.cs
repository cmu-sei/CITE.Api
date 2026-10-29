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
using Crucible.Common.EntityEvents.Events;
using Cite.Api.Data;
using Cite.Api.Data.Models;
using Cite.Api.Hubs;
using Cite.Api.Infrastructure.Authorization;
using Cite.Api.Infrastructure.Extensions;
using Cite.Api.Infrastructure.Options;
using Cite.Api.Services;

namespace Cite.Api.Infrastructure.EventHandlers
{
    public class BaseSubmissionHandler
    {
        protected readonly CiteContext _db;
        protected readonly IMapper _mapper;
        protected readonly ISubmissionService _submissionService;
        protected readonly IHubContext<MainHub> _mainHub;
        private readonly DatabaseOptions _options;

        public BaseSubmissionHandler(
            CiteContext db,
            IMapper mapper,
            ISubmissionService submissionService,
            IHubContext<MainHub> mainHub,
            DatabaseOptions options)
        {
            _db = db;
            _mapper = mapper;
            _submissionService = submissionService;
            _mainHub = mainHub;
            _options = options;
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
            // the official submission goes to the readers of that move's official score; a future move's to no team
            else if (submissionEntity.EvaluationId != null)
            {
                var currentMoveNumber = (await _db.Evaluations.FindAsync([submissionEntity.EvaluationId], cancellationToken)).CurrentMoveNumber;
                if (submissionEntity.MoveNumber < currentMoveNumber)
                {
                    signalrGroupIds.Add(submissionEntity.EvaluationId.ToString() + MainHub.PAST_OFFICIAL_SCORE_POSTFIX);
                }
                else if (submissionEntity.MoveNumber == currentMoveNumber)
                {
                    signalrGroupIds.Add(submissionEntity.EvaluationId.ToString() + MainHub.OFFICIAL_SCORE_POSTFIX);
                }
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
                tasks.Add(_mainHub.Clients.Group(signalrGroupId).SendAsync(method, submission, modifiedProperties, cancellationToken));
            }
            // a user's submission also updates the team average the team sees
            if (submissionEntity.UserId != null && submissionEntity.TeamId != null)
            {
                var averageSubmission = await _submissionService.GetTeamAverageAsync(submissionEntity, cancellationToken);
                if (averageSubmission != null)
                {
                    tasks.Add(_mainHub.Clients.Group(submissionEntity.TeamId.ToString()).SendAsync(method, averageSubmission, modifiedProperties, cancellationToken));
                }
            }

            await Task.WhenAll(tasks);
        }
    }

    public class SubmissionCreatedSignalRHandler : BaseSubmissionHandler, INotificationHandler<EntityCreated<SubmissionEntity>>
    {
        public SubmissionCreatedSignalRHandler(
            CiteContext db,
            IMapper mapper,
            ISubmissionService submissionService,
            IHubContext<MainHub> mainHub,
            DatabaseOptions options) : base(db, mapper, submissionService, mainHub, options)
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
            DatabaseOptions options) : base(db, mapper, submissionService, mainHub, options)
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
            DatabaseOptions options) : base(db, mapper, submissionService, mainHub, options)
        {
        }

        public async Task Handle(EntityDeleted<SubmissionEntity> notification, CancellationToken cancellationToken)
        {
            var signalrGroupIds = await base.GetSignalrGroupsForSubmissionAsync(notification.Entity, cancellationToken);
            var tasks = new List<Task>();

            foreach (var signalrGroupId in signalrGroupIds)
            {
                tasks.Add(_mainHub.Clients.Group(signalrGroupId).SendAsync(MainHubMethods.SubmissionDeleted, notification.Entity.Id, cancellationToken));
            }

            await Task.WhenAll(tasks);
        }
    }
}
