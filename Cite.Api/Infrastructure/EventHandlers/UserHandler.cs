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
    public class UserHandler
    {
        protected readonly IMapper _mapper;
        protected readonly IHubContext<MainHub> _mainHub;

        public UserHandler(
            IMapper mapper,
            IHubContext<MainHub> mainHub)
        {
            _mapper = mapper;
            _mainHub = mainHub;
        }

        protected string[] GetGroups(UserEntity userEntity)
        {
            var groupIds = new List<string>();
            groupIds.Add(userEntity.CreatedBy.ToString());
            // the admin data group gets everything
            groupIds.Add(MainHub.ADMIN_DATA_GROUP);

            return groupIds.ToArray();
        }

        protected async Task HandleCreateOrUpdate(
            UserEntity userEntity,
            string method,
            string[] modifiedProperties,
            CancellationToken cancellationToken)
        {
            var groupIds = this.GetGroups(userEntity);
            var user = _mapper.Map<ViewModels.User>(userEntity);
            var tasks = new List<Task>();

            foreach (var groupId in groupIds)
            {
                tasks.Add(_mainHub.Clients.Group(groupId).SendAsync(method, user, modifiedProperties, cancellationToken));
            }

            await Task.WhenAll(tasks);
        }
    }

    public class UserCreatedSignalRHandler : UserHandler, INotificationHandler<EntityCreated<UserEntity>>
    {
        public UserCreatedSignalRHandler(
            IMapper mapper,
            IHubContext<MainHub> mainHub) : base(mapper, mainHub) { }

        public async Task Handle(EntityCreated<UserEntity> notification, CancellationToken cancellationToken)
        {
            await base.HandleCreateOrUpdate(notification.Entity, MainHubMethods.UserCreated, null, cancellationToken);
        }
    }

    public class UserUpdatedSignalRHandler : UserHandler, INotificationHandler<EntityUpdated<UserEntity>>
    {
        public UserUpdatedSignalRHandler(
            IMapper mapper,
            IHubContext<MainHub> mainHub) : base(mapper, mainHub) { }

        public async Task Handle(EntityUpdated<UserEntity> notification, CancellationToken cancellationToken)
        {
            await base.HandleCreateOrUpdate(
                notification.Entity,
                MainHubMethods.UserUpdated,
                notification.ModifiedProperties.Select(x => x.TitleCaseToCamelCase()).ToArray(),
                cancellationToken);
        }
    }

    public class UserDeletedSignalRHandler : UserHandler, INotificationHandler<EntityDeleted<UserEntity>>
    {
        public UserDeletedSignalRHandler(
            IMapper mapper,
            IHubContext<MainHub> mainHub) : base(mapper, mainHub)
        {
        }

        public async Task Handle(EntityDeleted<UserEntity> notification, CancellationToken cancellationToken)
        {
            var groupIds = base.GetGroups(notification.Entity);
            var tasks = new List<Task>();

            foreach (var groupId in groupIds)
            {
                tasks.Add(_mainHub.Clients.Group(groupId).SendAsync(MainHubMethods.UserDeleted, notification.Entity.Id, cancellationToken));
            }

            await Task.WhenAll(tasks);
        }
    }
}
