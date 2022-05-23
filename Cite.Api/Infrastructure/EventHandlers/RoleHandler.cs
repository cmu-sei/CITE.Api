// Copyright 2022 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license, please see LICENSE.md in the project root for license information or contact permission@sei.cmu.edu for full terms.

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using MediatR;
using Microsoft.AspNetCore.SignalR;
using Cite.Api.Data;
using Cite.Api.Data.Models;
using Cite.Api.Services;
using Cite.Api.Hubs;
using Cite.Api.Infrastructure.Extensions;

namespace Cite.Api.Infrastructure.EventHandlers
{
    public class BaseRoleHandler
    {
        protected readonly CiteContext _db;
        protected readonly IMapper _mapper;
        protected readonly IRoleService _roleService;
        protected readonly IHubContext<MainHub> _mainHub;

        public BaseRoleHandler(
            CiteContext db,
            IMapper mapper,
            IRoleService roleService,
            IHubContext<MainHub> mainHub)
        {
            _db = db;
            _mapper = mapper;
            _roleService = roleService;
            _mainHub = mainHub;
        }

        protected string[] GetGroups(RoleEntity roleEntity)
        {
            var groupIds = new List<string>();
            groupIds.Add(roleEntity.TeamId.ToString());
            // the admin data group gets everything
            groupIds.Add(MainHub.ADMIN_DATA_GROUP);

            return groupIds.ToArray();
        }

        protected async Task HandleCreateOrUpdate(
            RoleEntity roleEntity,
            string method,
            string[] modifiedProperties,
            CancellationToken cancellationToken)
        {
            var groupIds = GetGroups(roleEntity);
            var role = _mapper.Map<ViewModels.Role>(roleEntity);
            var tasks = new List<Task>();

            foreach (var groupId in groupIds)
            {
                tasks.Add(_mainHub.Clients.Group(groupId).SendAsync(MainHubMethods.RoleUpdated, role, modifiedProperties, cancellationToken));
            }

            await Task.WhenAll(tasks);
        }
    }

    public class RoleCreatedSignalRHandler : BaseRoleHandler, INotificationHandler<EntityCreated<RoleEntity>>
    {
        public RoleCreatedSignalRHandler(
            CiteContext db,
            IMapper mapper,
            IRoleService roleService,
            IHubContext<MainHub> mainHub) : base(db, mapper, roleService, mainHub) { }

        public async Task Handle(EntityCreated<RoleEntity> notification, CancellationToken cancellationToken)
        {
            await base.HandleCreateOrUpdate(notification.Entity, MainHubMethods.RoleCreated, null, cancellationToken);
        }
    }

    public class RoleUpdatedSignalRHandler : BaseRoleHandler, INotificationHandler<EntityUpdated<RoleEntity>>
    {
        public RoleUpdatedSignalRHandler(
            CiteContext db,
            IMapper mapper,
            IRoleService roleService,
            IHubContext<MainHub> mainHub) : base(db, mapper, roleService, mainHub) { }

        public async Task Handle(EntityUpdated<RoleEntity> notification, CancellationToken cancellationToken)
        {
            await base.HandleCreateOrUpdate(
                notification.Entity,
                MainHubMethods.RoleUpdated,
                notification.ModifiedProperties.Select(x => x.TitleCaseToCamelCase()).ToArray(),
                cancellationToken);
        }
    }

    public class RoleDeletedSignalRHandler : BaseRoleHandler, INotificationHandler<EntityDeleted<RoleEntity>>
    {
        public RoleDeletedSignalRHandler(
            CiteContext db,
            IMapper mapper,
            IRoleService roleService,
            IHubContext<MainHub> mainHub) : base(db, mapper, roleService, mainHub)
        {
        }

        public async Task Handle(EntityDeleted<RoleEntity> notification, CancellationToken cancellationToken)
        {
            var groupIds = base.GetGroups(notification.Entity);
            var tasks = new List<Task>();

            foreach (var groupId in groupIds)
            {
                tasks.Add(_mainHub.Clients.Group(groupId).SendAsync(MainHubMethods.RoleDeleted, notification.Entity.Id, cancellationToken));
            }

            await Task.WhenAll(tasks);
        }
    }
}
