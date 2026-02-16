// Copyright 2022 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license, please see LICENSE.md in the project root for license information or contact permission@sei.cmu.edu for full terms.

using System.Collections.Generic;
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

namespace Cite.Api.Infrastructure.EventHandlers
{
    public class ScoringCategoryHandler
    {
        protected readonly CiteContext _db;
        protected readonly IMapper _mapper;
        protected readonly IScoringCategoryService _ScoringCategoryService;
        protected readonly IHubContext<MainHub> _mainHub;

        public ScoringCategoryHandler(
            CiteContext db,
            IMapper mapper,
            IScoringCategoryService ScoringCategoryService,
            IHubContext<MainHub> mainHub)
        {
            _db = db;
            _mapper = mapper;
            _ScoringCategoryService = ScoringCategoryService;
            _mainHub = mainHub;
        }

        protected string[] GetGroups(ScoringCategoryEntity scoringCategoryEntity)
        {
            var groupIds = new List<string>();
            // add the scoringModel
            groupIds.Add(scoringCategoryEntity.ScoringModelId.ToString());
            // the admin data group gets everything
            groupIds.Add(MainHub.ADMIN_DATA_GROUP);

            return groupIds.ToArray();
        }

        protected async Task HandleChange(
            ScoringCategoryEntity scoringCategoryEntity,
            CancellationToken cancellationToken)
        {
            var groupIds = GetGroups(scoringCategoryEntity);
            var scoringModelEntity = await _db.ScoringModels.FindAsync(scoringCategoryEntity.ScoringModelId);
            var scoringModel = _mapper.Map<ViewModels.ScoringModel>(scoringModelEntity);
            var tasks = new List<Task>();

            foreach (var groupId in groupIds)
            {
                tasks.Add(_mainHub.Clients.Group(groupId).SendAsync(MainHubMethods.ScoringModelUpdated, scoringModel, null, cancellationToken));
            }

            await Task.WhenAll(tasks);
        }
    }

    public class ScoringCategoryCreatedSignalRHandler : ScoringCategoryHandler, INotificationHandler<EntityCreated<ScoringCategoryEntity>>
    {
        public ScoringCategoryCreatedSignalRHandler(
            CiteContext db,
            IMapper mapper,
            IScoringCategoryService scoringCategoryService,
            IHubContext<MainHub> mainHub) : base(db, mapper, scoringCategoryService, mainHub) { }

        public async Task Handle(EntityCreated<ScoringCategoryEntity> notification, CancellationToken cancellationToken)
        {
            await base.HandleChange(notification.Entity, cancellationToken);
        }
    }

    public class ScoringCategoryUpdatedSignalRHandler : ScoringCategoryHandler, INotificationHandler<EntityUpdated<ScoringCategoryEntity>>
    {
        public ScoringCategoryUpdatedSignalRHandler(
            CiteContext db,
            IMapper mapper,
            IScoringCategoryService scoringCategoryService,
            IHubContext<MainHub> mainHub) : base(db, mapper, scoringCategoryService, mainHub) { }

        public async Task Handle(EntityUpdated<ScoringCategoryEntity> notification, CancellationToken cancellationToken)
        {
            await base.HandleChange(notification.Entity, cancellationToken);
        }
    }

    public class ScoringCategoryDeletedSignalRHandler : ScoringCategoryHandler, INotificationHandler<EntityDeleted<ScoringCategoryEntity>>
    {
        public ScoringCategoryDeletedSignalRHandler(
            CiteContext db,
            IMapper mapper,
            IScoringCategoryService scoringCategoryService,
            IHubContext<MainHub> mainHub) : base(db, mapper, scoringCategoryService, mainHub)
        {
        }

        public async Task Handle(EntityDeleted<ScoringCategoryEntity> notification, CancellationToken cancellationToken)
        {
            await base.HandleChange(notification.Entity, cancellationToken);
        }
    }
}
