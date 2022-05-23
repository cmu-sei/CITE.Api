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
    public class EvaluationTeamHandler
    {
        protected readonly CiteContext _db;
        protected readonly IMapper _mapper;
        protected readonly IEvaluationTeamService _EvaluationTeamService;
        protected readonly IHubContext<MainHub> _mainHub;

        public EvaluationTeamHandler(
            CiteContext db,
            IMapper mapper,
            IEvaluationTeamService EvaluationTeamService,
            IHubContext<MainHub> mainHub)
        {
            _db = db;
            _mapper = mapper;
            _EvaluationTeamService = EvaluationTeamService;
            _mainHub = mainHub;
        }

        protected string[] GetGroups(EvaluationTeamEntity evaluationTeamEntity)
        {
            var groupIds = new List<string>();
            // add the evaluation
            groupIds.Add(evaluationTeamEntity.EvaluationId.ToString());
            // add the team
            groupIds.Add(evaluationTeamEntity.TeamId.ToString());
            // the admin data group gets everything
            groupIds.Add(MainHub.ADMIN_DATA_GROUP);

            return groupIds.ToArray();
        }

        protected async Task HandleChange(
            EvaluationTeamEntity evaluationTeamEntity,
            CancellationToken cancellationToken)
        {
            var groupIds = GetGroups(evaluationTeamEntity);
            var evaluationEntity = await _db.Evaluations.FindAsync(evaluationTeamEntity.EvaluationId);
            var evaluation = _mapper.Map<ViewModels.Evaluation>(evaluationEntity);
            var tasks = new List<Task>();

            foreach (var groupId in groupIds)
            {
                tasks.Add(_mainHub.Clients.Group(groupId).SendAsync(MainHubMethods.EvaluationUpdated, evaluation, null, cancellationToken));
            }

            await Task.WhenAll(tasks);
        }
    }

    public class EvaluationTeamCreatedSignalRHandler : EvaluationTeamHandler, INotificationHandler<EntityCreated<EvaluationTeamEntity>>
    {
        public EvaluationTeamCreatedSignalRHandler(
            CiteContext db,
            IMapper mapper,
            IEvaluationTeamService evaluationTeamService,
            IHubContext<MainHub> mainHub) : base(db, mapper, evaluationTeamService, mainHub) { }

        public async Task Handle(EntityCreated<EvaluationTeamEntity> notification, CancellationToken cancellationToken)
        {
            await base.HandleChange(notification.Entity, cancellationToken);
        }
    }

    public class EvaluationTeamUpdatedSignalRHandler : EvaluationTeamHandler, INotificationHandler<EntityUpdated<EvaluationTeamEntity>>
    {
        public EvaluationTeamUpdatedSignalRHandler(
            CiteContext db,
            IMapper mapper,
            IEvaluationTeamService evaluationTeamService,
            IHubContext<MainHub> mainHub) : base(db, mapper, evaluationTeamService, mainHub) { }

        public async Task Handle(EntityUpdated<EvaluationTeamEntity> notification, CancellationToken cancellationToken)
        {
            await base.HandleChange(notification.Entity, cancellationToken);
        }
    }

    public class EvaluationTeamDeletedSignalRHandler : EvaluationTeamHandler, INotificationHandler<EntityDeleted<EvaluationTeamEntity>>
    {
        public EvaluationTeamDeletedSignalRHandler(
            CiteContext db,
            IMapper mapper,
            IEvaluationTeamService evaluationTeamService,
            IHubContext<MainHub> mainHub) : base(db, mapper, evaluationTeamService, mainHub)
        {
        }

        public async Task Handle(EntityDeleted<EvaluationTeamEntity> notification, CancellationToken cancellationToken)
        {
            await base.HandleChange(notification.Entity, cancellationToken);
        }
    }
}
