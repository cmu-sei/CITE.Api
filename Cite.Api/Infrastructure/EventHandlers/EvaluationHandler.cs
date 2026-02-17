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
    public class BaseEvaluationHandler
    {
        protected readonly CiteContext _db;
        protected readonly IMapper _mapper;
        protected readonly IEvaluationService _evaluationService;
        protected readonly IHubContext<MainHub> _mainHub;

        public BaseEvaluationHandler(
            CiteContext db,
            IMapper mapper,
            IEvaluationService evaluationService,
            IHubContext<MainHub> mainHub)
        {
            _db = db;
            _mapper = mapper;
            _evaluationService = evaluationService;
            _mainHub = mainHub;
        }

        protected string[] GetGroups(EvaluationEntity evaluationEntity)
        {
            var groupIds = new List<string>();
            groupIds.Add(evaluationEntity.Id.ToString());
            // the admin data group gets everything
            groupIds.Add(MainHub.ADMIN_DATA_GROUP);

            return groupIds.ToArray();
        }

        protected async Task HandleCreateOrUpdate(
            EvaluationEntity evaluationEntity,
            string method,
            string[] modifiedProperties,
            CancellationToken cancellationToken)
        {
            var groupIds = GetGroups(evaluationEntity);
            var evaluation = _mapper.Map<ViewModels.Evaluation>(evaluationEntity);
            var tasks = new List<Task>();

            foreach (var groupId in groupIds)
            {
                tasks.Add(_mainHub.Clients.Group(groupId).SendAsync(method, evaluation, modifiedProperties, cancellationToken));
            }

            await Task.WhenAll(tasks);
        }
    }

    public class EvaluationCreatedSignalRHandler : BaseEvaluationHandler, INotificationHandler<EntityCreated<EvaluationEntity>>
    {
        public EvaluationCreatedSignalRHandler(
            CiteContext db,
            IMapper mapper,
            IEvaluationService evaluationService,
            IHubContext<MainHub> mainHub) : base(db, mapper, evaluationService, mainHub) { }

        public async Task Handle(EntityCreated<EvaluationEntity> notification, CancellationToken cancellationToken)
        {
            await base.HandleCreateOrUpdate(notification.Entity, MainHubMethods.EvaluationCreated, null, cancellationToken);
        }
    }

    public class EvaluationUpdatedSignalRHandler : BaseEvaluationHandler, INotificationHandler<EntityUpdated<EvaluationEntity>>
    {
        public EvaluationUpdatedSignalRHandler(
            CiteContext db,
            IMapper mapper,
            IEvaluationService evaluationService,
            IHubContext<MainHub> mainHub) : base(db, mapper, evaluationService, mainHub) { }

        public async Task Handle(EntityUpdated<EvaluationEntity> notification, CancellationToken cancellationToken)
        {
            await base.HandleCreateOrUpdate(
                notification.Entity,
                MainHubMethods.EvaluationUpdated,
                notification.ModifiedProperties.Select(x => x.TitleCaseToCamelCase()).ToArray(),
                cancellationToken);
        }
    }

    public class EvaluationDeletedSignalRHandler : BaseEvaluationHandler, INotificationHandler<EntityDeleted<EvaluationEntity>>
    {
        public EvaluationDeletedSignalRHandler(
            CiteContext db,
            IMapper mapper,
            IEvaluationService evaluationService,
            IHubContext<MainHub> mainHub) : base(db, mapper, evaluationService, mainHub)
        {
        }

        public async Task Handle(EntityDeleted<EvaluationEntity> notification, CancellationToken cancellationToken)
        {
            var groupIds = base.GetGroups(notification.Entity);
            var tasks = new List<Task>();

            foreach (var groupId in groupIds)
            {
                tasks.Add(_mainHub.Clients.Group(groupId).SendAsync(MainHubMethods.EvaluationDeleted, notification.Entity.Id, cancellationToken));
            }

            await Task.WhenAll(tasks);
        }
    }
}
