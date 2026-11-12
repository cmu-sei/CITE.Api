// Copyright 2022 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license, please see LICENSE.md in the project root for license information or contact permission@sei.cmu.edu for full terms.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.SignalR;
using Crucible.Common.EntityEvents.Events;
using Cite.Api.Data;
using Cite.Api.Data.Models;
using Cite.Api.Services;
using Cite.Api.Hubs;
using Cite.Api.Infrastructure.Mappings;

namespace Cite.Api.Infrastructure.EventHandlers
{
    public class ScoringOptionHandler
    {
        protected readonly CiteContext _db;
        protected readonly IScoringOptionService _ScoringOptionService;
        protected readonly IHubContext<MainHub> _mainHub;

        public ScoringOptionHandler(
            CiteContext db,
            IScoringOptionService ScoringOptionService,
            IHubContext<MainHub> mainHub)
        {
            _db = db;
            _ScoringOptionService = ScoringOptionService;
            _mainHub = mainHub;
        }

        protected async Task<string[]> GetGroups(ScoringOptionEntity scoringOptionEntity, CancellationToken cancellationToken)
        {
            var groupIds = new List<string>();
            var scoringModelId = (await _db.ScoringCategories.FindAsync(scoringOptionEntity.ScoringCategoryId)).ScoringModelId;
            // add the scoringModel
            groupIds.Add(scoringModelId.ToString());
            // the admin data group gets everything
            groupIds.Add(MainHub.ADMIN_DATA_GROUP);

            return groupIds.ToArray();
        }

        protected async Task HandleChange(
            ScoringOptionEntity scoringOptionEntity,
            CancellationToken cancellationToken)
        {
            var groupIds = await this.GetGroups(scoringOptionEntity, cancellationToken);
            var scoringCategoryEntity = await _db.ScoringCategories.FindAsync(scoringOptionEntity.ScoringCategoryId);
            var scoringModelEntity = await _db.ScoringModels.FindAsync(scoringCategoryEntity.ScoringModelId);
            var scoringModel = scoringModelEntity.ToScoringModel();
            var tasks = new List<Task>();

            foreach (var groupId in groupIds)
            {
                tasks.Add(_mainHub.Clients.Group(groupId).SendAsync(MainHubMethods.ScoringModelUpdated, scoringModel, null, cancellationToken));
            }

            await Task.WhenAll(tasks);
        }
    }

    public class ScoringOptionCreatedSignalRHandler : ScoringOptionHandler, INotificationHandler<EntityCreated<ScoringOptionEntity>>
    {
        public ScoringOptionCreatedSignalRHandler(
            CiteContext db,
            IScoringOptionService scoringOptionService,
            IHubContext<MainHub> mainHub) : base(db, scoringOptionService, mainHub) { }

        public async Task Handle(EntityCreated<ScoringOptionEntity> notification, CancellationToken cancellationToken)
        {
            await base.HandleChange(notification.Entity, cancellationToken);
        }
    }

    public class ScoringOptionUpdatedSignalRHandler : ScoringOptionHandler, INotificationHandler<EntityUpdated<ScoringOptionEntity>>
    {
        public ScoringOptionUpdatedSignalRHandler(
            CiteContext db,
            IScoringOptionService scoringOptionService,
            IHubContext<MainHub> mainHub) : base(db, scoringOptionService, mainHub) { }

        public async Task Handle(EntityUpdated<ScoringOptionEntity> notification, CancellationToken cancellationToken)
        {
            await base.HandleChange(notification.Entity, cancellationToken);
        }
    }

    public class ScoringOptionDeletedSignalRHandler : ScoringOptionHandler, INotificationHandler<EntityDeleted<ScoringOptionEntity>>
    {
        public ScoringOptionDeletedSignalRHandler(
            CiteContext db,
            IScoringOptionService scoringOptionService,
            IHubContext<MainHub> mainHub) : base(db, scoringOptionService, mainHub)
        {
        }

        public async Task Handle(EntityDeleted<ScoringOptionEntity> notification, CancellationToken cancellationToken)
        {
            await base.HandleChange(notification.Entity, cancellationToken);
        }
    }
}
