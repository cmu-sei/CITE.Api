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
    public class BaseActionHandler
    {
        protected readonly CiteContext _db;
        protected readonly IMapper _mapper;
        protected readonly IActionService _actionService;
        protected readonly IHubContext<MainHub> _mainHub;

        public BaseActionHandler(
            CiteContext db,
            IMapper mapper,
            IActionService actionService,
            IHubContext<MainHub> mainHub)
        {
            _db = db;
            _mapper = mapper;
            _actionService = actionService;
            _mainHub = mainHub;
        }

        protected string[] GetGroups(ActionEntity actionEntity)
        {
            var groupIds = new List<string>();
            groupIds.Add(actionEntity.TeamId.ToString());
            // the admin data group gets everything
            groupIds.Add(MainHub.ADMIN_DATA_GROUP);

            return groupIds.ToArray();
        }

        protected async Task HandleCreateOrUpdate(
            ActionEntity actionEntity,
            string method,
            string[] modifiedProperties,
            CancellationToken cancellationToken)
        {
            var groupIds = this.GetGroups(actionEntity);
            var action = _mapper.Map<ViewModels.Action>(actionEntity);
            var tasks = new List<Task>();

            foreach (var groupId in groupIds)
            {
                tasks.Add(_mainHub.Clients.Group(groupId).SendAsync(method, action, modifiedProperties, cancellationToken));
            }

            await Task.WhenAll(tasks);
        }
    }

    public class ActionCreatedSignalRHandler : BaseActionHandler, INotificationHandler<EntityCreated<ActionEntity>>
    {
        public ActionCreatedSignalRHandler(
            CiteContext db,
            IMapper mapper,
            IActionService actionService,
            IHubContext<MainHub> mainHub) : base(db, mapper, actionService, mainHub) { }

        public async Task Handle(EntityCreated<ActionEntity> notification, CancellationToken cancellationToken)
        {
            await base.HandleCreateOrUpdate(notification.Entity, MainHubMethods.ActionCreated, null, cancellationToken);
        }
    }

    public class ActionUpdatedSignalRHandler : BaseActionHandler, INotificationHandler<EntityUpdated<ActionEntity>>
    {
        public ActionUpdatedSignalRHandler(
            CiteContext db,
            IMapper mapper,
            IActionService actionService,
            IHubContext<MainHub> mainHub) : base(db, mapper, actionService, mainHub) { }

        public async Task Handle(EntityUpdated<ActionEntity> notification, CancellationToken cancellationToken)
        {
            await base.HandleCreateOrUpdate(
                notification.Entity,
                MainHubMethods.ActionUpdated,
                notification.ModifiedProperties.Select(x => x.TitleCaseToCamelCase()).ToArray(),
                cancellationToken);
        }
    }

    public class ActionDeletedSignalRHandler : BaseActionHandler, INotificationHandler<EntityDeleted<ActionEntity>>
    {
        public ActionDeletedSignalRHandler(
            CiteContext db,
            IMapper mapper,
            IActionService actionService,
            IHubContext<MainHub> mainHub) : base(db, mapper, actionService, mainHub)
        {
        }

        public async Task Handle(EntityDeleted<ActionEntity> notification, CancellationToken cancellationToken)
        {
            var groupIds = base.GetGroups(notification.Entity);
            var tasks = new List<Task>();

            foreach (var groupId in groupIds)
            {
                tasks.Add(_mainHub.Clients.Group(groupId).SendAsync(MainHubMethods.ActionDeleted, notification.Entity.Id, cancellationToken));
            }

            await Task.WhenAll(tasks);
        }
    }
}
