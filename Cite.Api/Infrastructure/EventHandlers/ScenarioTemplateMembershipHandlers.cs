// Copyright 2024 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.

using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using MediatR;
using Microsoft.AspNetCore.SignalR;
using Cite.Api.Data.Models;
using Cite.Api.Hubs;

namespace Cite.Api.Infrastructure.EventHandlers
{
    public class ScoringModelMembershipCreatedSignalRHandler : INotificationHandler<EntityCreated<ScoringModelMembershipEntity>>
    {
        private readonly IHubContext<MainHub> _mainHub;
        private readonly IMapper _mapper;

        public ScoringModelMembershipCreatedSignalRHandler(
            IHubContext<MainHub> mainHub,
            IMapper mapper)
        {
            _mainHub = mainHub;
            _mapper = mapper;
        }

        public async Task Handle(EntityCreated<ScoringModelMembershipEntity> notification, CancellationToken cancellationToken)
        {
            var scoringModelMembership = _mapper.Map<ViewModels.ScoringModelMembership>(notification.Entity);
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
        private readonly IMapper _mapper;

        public ScoringModelMembershipUpdatedSignalRHandler(
            IHubContext<MainHub> mainHub,
            IMapper mapper)
        {
            _mainHub = mainHub;
            _mapper = mapper;
        }

        public async Task Handle(EntityUpdated<ScoringModelMembershipEntity> notification, CancellationToken cancellationToken)
        {
            var scoringModelMembership = _mapper.Map<ViewModels.ScoringModelMembership>(notification.Entity);
            await _mainHub.Clients
                .Groups(MainHub.SCORING_MODEL_GROUP)
                .SendAsync(MainHubMethods.ScoringModelMembershipUpdated, scoringModelMembership);
        }
    }
}
