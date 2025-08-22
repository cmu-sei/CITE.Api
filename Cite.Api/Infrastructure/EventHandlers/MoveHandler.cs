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
    public class MoveHandler
    {
        protected readonly CiteContext _db;
        protected readonly IMapper _mapper;
        protected readonly IMoveService _moveService;
        protected readonly IHubContext<MainHub> _mainHub;

        public MoveHandler(
            CiteContext db,
            IMapper mapper,
            IMoveService moveService,
            IHubContext<MainHub> mainHub)
        {
            _db = db;
            _mapper = mapper;
            _moveService = moveService;
            _mainHub = mainHub;
        }

        protected string[] GetGroups(MoveEntity moveEntity)
        {
            var groupIds = new List<string>();
            groupIds.Add(moveEntity.EvaluationId.ToString());
            // the admin data group gets everything
            groupIds.Add(MainHub.ADMIN_DATA_GROUP);

            return groupIds.ToArray();
        }

        protected async Task HandleCreateOrUpdate(
            MoveEntity moveEntity,
            string method,
            string[] modifiedProperties,
            CancellationToken cancellationToken)
        {
            var groupIds = GetGroups(moveEntity);
            var move = _mapper.Map<ViewModels.Move>(moveEntity);
            var tasks = new List<Task>();

            foreach (var groupId in groupIds)
            {
                tasks.Add(_mainHub.Clients.Group(groupId).SendAsync(method, move, modifiedProperties, cancellationToken));
            }

            await Task.WhenAll(tasks);
        }
    }

    public class MoveCreatedSignalRHandler : MoveHandler, INotificationHandler<EntityCreated<MoveEntity>>
    {
        public MoveCreatedSignalRHandler(
            CiteContext db,
            IMapper mapper,
            IMoveService moveService,
            IHubContext<MainHub> mainHub) : base(db, mapper, moveService, mainHub) { }

        public async Task Handle(EntityCreated<MoveEntity> notification, CancellationToken cancellationToken)
        {
            await base.HandleCreateOrUpdate(notification.Entity, MainHubMethods.MoveCreated, null, cancellationToken);
        }
    }

    public class MoveUpdatedSignalRHandler : MoveHandler, INotificationHandler<EntityUpdated<MoveEntity>>
    {
        public MoveUpdatedSignalRHandler(
            CiteContext db,
            IMapper mapper,
            IMoveService moveService,
            IHubContext<MainHub> mainHub) : base(db, mapper, moveService, mainHub) { }

        public async Task Handle(EntityUpdated<MoveEntity> notification, CancellationToken cancellationToken)
        {
            await base.HandleCreateOrUpdate(
                notification.Entity,
                MainHubMethods.MoveUpdated,
                notification.ModifiedProperties.Select(x => x.TitleCaseToCamelCase()).ToArray(),
                cancellationToken);
        }
    }

    public class MoveDeletedSignalRHandler : MoveHandler, INotificationHandler<EntityDeleted<MoveEntity>>
    {
        public MoveDeletedSignalRHandler(
            CiteContext db,
            IMapper mapper,
            IMoveService moveService,
            IHubContext<MainHub> mainHub) : base(db, mapper, moveService, mainHub)
        {
        }

        public async Task Handle(EntityDeleted<MoveEntity> notification, CancellationToken cancellationToken)
        {
            var groupIds = base.GetGroups(notification.Entity);
            var tasks = new List<Task>();

            foreach (var groupId in groupIds)
            {
                tasks.Add(_mainHub.Clients.Group(groupId).SendAsync(MainHubMethods.MoveDeleted, notification.Entity.Id, cancellationToken));
            }

            await Task.WhenAll(tasks);
        }
    }

    public class MoveCreatedSubmissionHandler(ISubmissionService submissionService, CiteContext db) : INotificationHandler<EntityCreated<MoveEntity>>
    {
        public async Task Handle(EntityCreated<MoveEntity> notification, CancellationToken cancellationToken)
        {
            await submissionService.CreateMoveSubmissions(notification.Entity, db, cancellationToken);
        }
    }

}
