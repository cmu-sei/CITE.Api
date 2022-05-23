// Copyright 2022 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license, please see LICENSE.md in the project root for license information or contact permission@sei.cmu.edu for full terms.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using MediatR;
using Microsoft.AspNetCore.SignalR;
using Cite.Api.Data;
using Cite.Api.Data.Models;
using Cite.Api.Services;
using Cite.Api.Hubs;

namespace Cite.Api.Infrastructure.EventHandlers
{
    public class BaseRoleUserHandler
    {
        protected readonly CiteContext _db;
        protected readonly IMapper _mapper;
        protected readonly IRoleService _roleService;
        protected readonly IHubContext<MainHub> _mainHub;

        public BaseRoleUserHandler(
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

        protected string[] GetGroups(RoleUserEntity roleUserEntity)
        {
            var groupIds = new List<string>();
            groupIds.Add(roleUserEntity.Role.TeamId.ToString());
            // the admin data group gets everything
            groupIds.Add(MainHub.ADMIN_DATA_GROUP);

            return groupIds.ToArray();
        }

        protected async Task HandleCreateUpdateDelete(
            RoleUserEntity roleUserEntity,
            string method,
            string[] modifiedProperties,
            CancellationToken cancellationToken)
        {
            var groupIds = GetGroups(roleUserEntity);
            var role = _mapper.Map<ViewModels.Role>(roleUserEntity.Role);
            var tasks = new List<Task>();

            foreach (var groupId in groupIds)
            {
                tasks.Add(_mainHub.Clients.Group(groupId).SendAsync(method, role, modifiedProperties, cancellationToken));
            }

            await Task.WhenAll(tasks);
        }
    }

    public class RoleUserCreatedSignalRHandler : BaseRoleUserHandler, INotificationHandler<EntityCreated<RoleUserEntity>>
    {
        public RoleUserCreatedSignalRHandler(
            CiteContext db,
            IMapper mapper,
            IRoleService roleService,
            IHubContext<MainHub> mainHub) : base(db, mapper, roleService, mainHub) { }

        public async Task Handle(EntityCreated<RoleUserEntity> notification, CancellationToken cancellationToken)
        {
            await base.HandleCreateUpdateDelete(notification.Entity, MainHubMethods.RoleUpdated, null, cancellationToken);
        }
    }

    public class RoleUserUpdatedSignalRHandler : BaseRoleUserHandler, INotificationHandler<EntityUpdated<RoleUserEntity>>
    {
        public RoleUserUpdatedSignalRHandler(
            CiteContext db,
            IMapper mapper,
            IRoleService roleService,
            IHubContext<MainHub> mainHub) : base(db, mapper, roleService, mainHub) { }

        public async Task Handle(EntityUpdated<RoleUserEntity> notification, CancellationToken cancellationToken)
        {
            await base.HandleCreateUpdateDelete(notification.Entity, MainHubMethods.RoleUpdated, null, cancellationToken);
        }
    }

    public class RoleUserDeletedSignalRHandler : BaseRoleUserHandler, INotificationHandler<EntityDeleted<RoleUserEntity>>
    {
        public RoleUserDeletedSignalRHandler(
            CiteContext db,
            IMapper mapper,
            IRoleService roleService,
            IHubContext<MainHub> mainHub) : base(db, mapper, roleService, mainHub)
        {
        }

        public async Task Handle(EntityDeleted<RoleUserEntity> notification, CancellationToken cancellationToken)
        {
            var groupIds = base.GetGroups(notification.Entity);
            var tasks = new List<Task>();
            // for some reason, the deleted RoleUser is still shown in notification.Entity.Role.RoleUsers, so we remove it to send correct info to clients
            notification.Entity.Role.RoleUsers.Remove(notification.Entity);

            foreach (var groupId in groupIds)
            {
                await base.HandleCreateUpdateDelete(notification.Entity, MainHubMethods.RoleUpdated, null, cancellationToken);
            }

            await Task.WhenAll(tasks);
        }
    }
}
