// Copyright 2024 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.

using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.SignalR;
using Crucible.Common.EntityEvents.Events;
using Cite.Api.Data.Models;
using Cite.Api.Hubs;
using Cite.Api.Infrastructure.Mappings;

namespace Cite.Api.Infrastructure.EventHandlers
{
    public class ScoringModelMembershipCreatedSignalRHandler : INotificationHandler<EntityCreated<ScoringModelMembershipEntity>>
    {
        private readonly IHubContext<MainHub> _mainHub;

        public ScoringModelMembershipCreatedSignalRHandler(
            IHubContext<MainHub> mainHub)
        {
            _mainHub = mainHub;
        }

        public async Task Handle(EntityCreated<ScoringModelMembershipEntity> notification, CancellationToken cancellationToken)
        {
            var scoringModelMembership = notification.Entity.ToScoringModelMembership();
            await _mainHub.Clients
                .Groups(MainHub.SCORING_MODEL_GROUP)
                .SendAsync(MainHubMethods.ScoringModelMembershipCreated, scoringModelMembership);
        }
    }

    public class ScoringModelMembershipDeletedSignalRHandler : INotificationHandler<EntityDeleted<ScoringModelMembershipEntity>>
    {
        private readonly IHubContext<MainHub> _mainHub;

        public ScoringModelMembershipDeletedSignalRHandler(
            IHubContext<MainHub> mainHub)
        {
            _mainHub = mainHub;
        }

        public async Task Handle(EntityDeleted<ScoringModelMembershipEntity> notification, CancellationToken cancellationToken)
        {
            await _mainHub.Clients
                .Groups(MainHub.SCORING_MODEL_GROUP)
                .SendAsync(MainHubMethods.ScoringModelMembershipDeleted, notification.Entity.Id);
        }
    }

    public class ScoringModelMembershipUpdatedSignalRHandler : INotificationHandler<EntityUpdated<ScoringModelMembershipEntity>>
    {
        private readonly IHubContext<MainHub> _mainHub;

        public ScoringModelMembershipUpdatedSignalRHandler(
            IHubContext<MainHub> mainHub)
        {
            _mainHub = mainHub;
        }

        public async Task Handle(EntityUpdated<ScoringModelMembershipEntity> notification, CancellationToken cancellationToken)
        {
            var scoringModelMembership = notification.Entity.ToScoringModelMembership();
            await _mainHub.Clients
                .Groups(MainHub.SCORING_MODEL_GROUP)
                .SendAsync(MainHubMethods.ScoringModelMembershipUpdated, scoringModelMembership);
        }
    }
}
