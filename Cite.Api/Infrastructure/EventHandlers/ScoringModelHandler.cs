// Copyright 2022 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license, please see LICENSE.md in the project root for license information or contact permission@sei.cmu.edu for full terms.

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using MediatR;
using Microsoft.AspNetCore.SignalR;
using Crucible.Common.EntityEvents.Events;
using Cite.Api.Data;
using Cite.Api.Data.Models;
using Cite.Api.Services;
using Cite.Api.Hubs;
using Cite.Api.Infrastructure.Extensions;

namespace Cite.Api.Infrastructure.EventHandlers
{
    public class BaseScoringModelHandler
    {
        protected readonly CiteContext _db;
        protected readonly IMapper _mapper;
        protected readonly IScoringModelService _scoringModelService;
        protected readonly IHubContext<MainHub> _mainHub;

        public BaseScoringModelHandler(
            CiteContext db,
            IMapper mapper,
            IScoringModelService scoringModelService,
            IHubContext<MainHub> mainHub)
        {
            _db = db;
            _mapper = mapper;
            _scoringModelService = scoringModelService;
            _mainHub = mainHub;
        }

        protected string[] GetGroups(ScoringModelEntity scoringModelEntity)
        {
            var groupIds = new List<string>();
            groupIds.Add(scoringModelEntity.Id.ToString());
            // the admin data group gets everything
            groupIds.Add(MainHub.ADMIN_DATA_GROUP);

            return groupIds.ToArray();
        }

        protected async Task HandleCreateOrUpdate(
            ScoringModelEntity scoringModelEntity,
            string method,
            string[] modifiedProperties,
            CancellationToken cancellationToken)
        {
            var groupIds = GetGroups(scoringModelEntity);
            var scoringModel = _mapper.Map<ViewModels.ScoringModel>(scoringModelEntity);
            var tasks = new List<Task>();

            foreach (var groupId in groupIds)
            {
                tasks.Add(_mainHub.Clients.Group(groupId).SendAsync(method, scoringModel, modifiedProperties, cancellationToken));
            }

            await Task.WhenAll(tasks);
        }
    }

    public class ScoringModelCreatedSignalRHandler : BaseScoringModelHandler, INotificationHandler<EntityCreated<ScoringModelEntity>>
    {
        public ScoringModelCreatedSignalRHandler(
            CiteContext db,
            IMapper mapper,
            IScoringModelService scoringModelService,
            IHubContext<MainHub> mainHub) : base(db, mapper, scoringModelService, mainHub) { }

        public async Task Handle(EntityCreated<ScoringModelEntity> notification, CancellationToken cancellationToken)
        {
            await base.HandleCreateOrUpdate(notification.Entity, MainHubMethods.ScoringModelCreated, null, cancellationToken);
        }
    }

    public class ScoringModelUpdatedSignalRHandler : BaseScoringModelHandler, INotificationHandler<EntityUpdated<ScoringModelEntity>>
    {
        public ScoringModelUpdatedSignalRHandler(
            CiteContext db,
            IMapper mapper,
            IScoringModelService scoringModelService,
            IHubContext<MainHub> mainHub) : base(db, mapper, scoringModelService, mainHub) { }

        public async Task Handle(EntityUpdated<ScoringModelEntity> notification, CancellationToken cancellationToken)
        {
            await base.HandleCreateOrUpdate(
                notification.Entity,
                MainHubMethods.ScoringModelUpdated,
                notification.ModifiedProperties.Select(x => x.TitleCaseToCamelCase()).ToArray(),
                cancellationToken);
        }
    }

    public class ScoringModelDeletedSignalRHandler : BaseScoringModelHandler, INotificationHandler<EntityDeleted<ScoringModelEntity>>
    {
        public ScoringModelDeletedSignalRHandler(
            CiteContext db,
            IMapper mapper,
            IScoringModelService scoringModelService,
            IHubContext<MainHub> mainHub) : base(db, mapper, scoringModelService, mainHub)
        {
        }

        public async Task Handle(EntityDeleted<ScoringModelEntity> notification, CancellationToken cancellationToken)
        {
            var groupIds = base.GetGroups(notification.Entity);
            var tasks = new List<Task>();

            foreach (var groupId in groupIds)
            {
                tasks.Add(_mainHub.Clients.Group(groupId).SendAsync(MainHubMethods.ScoringModelDeleted, notification.Entity.Id, cancellationToken));
            }

            await Task.WhenAll(tasks);
        }
    }
}
