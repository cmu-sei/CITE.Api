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
    public class EvaluationMembershipCreatedSignalRHandler : INotificationHandler<EntityCreated<EvaluationMembershipEntity>>
    {
        private readonly IHubContext<MainHub> _mainHub;

        public EvaluationMembershipCreatedSignalRHandler(
            IHubContext<MainHub> mainHub)
        {
            _mainHub = mainHub;
        }

        public async Task Handle(EntityCreated<EvaluationMembershipEntity> notification, CancellationToken cancellationToken)
        {
            var evaluationMembership = notification.Entity.ToEvaluationMembership();
            await _mainHub.Clients
                .Groups(MainHub.EVALUATION_GROUP)
                .SendAsync(MainHubMethods.EvaluationMembershipCreated, evaluationMembership);
        }
    }

    public class EvaluationMembershipDeletedSignalRHandler : INotificationHandler<EntityDeleted<EvaluationMembershipEntity>>
    {
        private readonly IHubContext<MainHub> _mainHub;

        public EvaluationMembershipDeletedSignalRHandler(
            IHubContext<MainHub> mainHub)
        {
            _mainHub = mainHub;
        }

        public async Task Handle(EntityDeleted<EvaluationMembershipEntity> notification, CancellationToken cancellationToken)
        {
            await _mainHub.Clients
                .Groups(MainHub.EVALUATION_GROUP)
                .SendAsync(MainHubMethods.EvaluationMembershipDeleted, notification.Entity.Id);
        }
    }

    public class EvaluationMembershipUpdatedSignalRHandler : INotificationHandler<EntityUpdated<EvaluationMembershipEntity>>
    {
        private readonly IHubContext<MainHub> _mainHub;

        public EvaluationMembershipUpdatedSignalRHandler(
            IHubContext<MainHub> mainHub)
        {
            _mainHub = mainHub;
        }

        public async Task Handle(EntityUpdated<EvaluationMembershipEntity> notification, CancellationToken cancellationToken)
        {
            var evaluationMembership = notification.Entity.ToEvaluationMembership();
            await _mainHub.Clients
                .Groups(MainHub.EVALUATION_GROUP)
                .SendAsync(MainHubMethods.EvaluationMembershipUpdated, evaluationMembership);
        }
    }
}
