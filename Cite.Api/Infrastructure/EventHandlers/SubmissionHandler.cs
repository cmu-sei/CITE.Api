// Copyright 2022 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license, please see LICENSE.md in the project root for license information or contact permission@sei.cmu.edu for full terms.

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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
using Cite.Api.Infrastructure.Mappings;

namespace Cite.Api.Infrastructure.EventHandlers
{
    public class BaseSubmissionHandler
    {
        protected readonly CiteContext _db;
        protected readonly ISubmissionService _submissionService;
        protected readonly IHubContext<MainHub> _mainHub;
        private readonly DatabaseOptions _options;

        public BaseSubmissionHandler(
            CiteContext db,
            ISubmissionService submissionService,
            IHubContext<MainHub> mainHub,
            DatabaseOptions options)
        {
            _db = db;
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

            var submission = fullEntity.ToSubmission();
            var tasks = new List<Task>();
            // on an evaluation that hides names, the team's group is split: its members (and anyone else
            // ObserverNamesService does not mask) get the names, everyone else watching the team gets none
            var teamGroupId = submissionEntity.TeamId?.ToString();
            var hidesNames = teamGroupId != null &&
                await ObserverNamesService.EvaluationHidesNamesAsync(_db, submissionEntity.EvaluationId, cancellationToken);

            foreach (var signalrGroupId in signalrGroupIds)
            {
                if (hidesNames && signalrGroupId == teamGroupId)
                {
                    tasks.Add(SendWithAndWithoutNames(teamGroupId, method, submission, modifiedProperties, cancellationToken));
                    continue;
                }
                tasks.Add(_mainHub.Clients.Group(signalrGroupId).SendAsync(method, submission, modifiedProperties, cancellationToken));
            }
            // a user's submission also updates the team average the team sees
            if (submissionEntity.UserId != null && submissionEntity.TeamId != null)
            {
                var averageSubmission = await _submissionService.GetTeamAverageAsync(submissionEntity, cancellationToken);
                if (averageSubmission != null)
                {
                    if (hidesNames)
                    {
                        tasks.Add(SendWithAndWithoutNames(teamGroupId, method, averageSubmission, modifiedProperties, cancellationToken));
                    }
                    else
                    {
                        tasks.Add(_mainHub.Clients.Group(teamGroupId).SendAsync(method, averageSubmission, modifiedProperties, cancellationToken));
                    }
                }
            }

            await Task.WhenAll(tasks);
        }

        /// <summary>The submission with its names to the team's named group, and a copy without them to its unnamed group.</summary>
        private Task SendWithAndWithoutNames(
            string teamGroupId,
            string method,
            ViewModels.Submission submission,
            string[] modifiedProperties,
            CancellationToken cancellationToken)
        {
            var masked = ObserverNamesService.CopyWithoutNames(submission);
            return Task.WhenAll(
                _mainHub.Clients.Group(teamGroupId + MainHub.NAMES_SHOWN_POSTFIX).SendAsync(method, submission, modifiedProperties, cancellationToken),
                _mainHub.Clients.Group(teamGroupId + MainHub.NAMES_HIDDEN_POSTFIX).SendAsync(method, masked, modifiedProperties, cancellationToken));
        }
    }

    public class SubmissionCreatedSignalRHandler : BaseSubmissionHandler, INotificationHandler<EntityCreated<SubmissionEntity>>
    {
        public SubmissionCreatedSignalRHandler(
            CiteContext db,
            ISubmissionService submissionService,
            IHubContext<MainHub> mainHub,
            DatabaseOptions options) : base(db, submissionService, mainHub, options)
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
            ISubmissionService submissionService,
            IHubContext<MainHub> mainHub,
            DatabaseOptions options) : base(db, submissionService, mainHub, options)
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
            ISubmissionService submissionService,
            IHubContext<MainHub> mainHub,
            DatabaseOptions options) : base(db, submissionService, mainHub, options)
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
