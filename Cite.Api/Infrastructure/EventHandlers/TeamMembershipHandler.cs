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
    public class TeamMembershipHandler
    {
        protected readonly CiteContext _db;
        protected readonly IMapper _mapper;
        protected readonly ITeamMembershipService _TeamMembershipService;
        protected readonly IHubContext<MainHub> _mainHub;

        public TeamMembershipHandler(
            CiteContext db,
            IMapper mapper,
            ITeamMembershipService TeamMembershipService,
            IHubContext<MainHub> mainHub)
        {
            _db = db;
            _mapper = mapper;
            _TeamMembershipService = TeamMembershipService;
            _mainHub = mainHub;
        }

        protected string[] GetGroups(TeamMembershipEntity teamMembershipEntity)
        {
            var groupIds = new List<string>();
            // add the team
            groupIds.Add(teamMembershipEntity.TeamId.ToString());
            // add the user
            groupIds.Add(teamMembershipEntity.UserId.ToString());
            // the admin data group gets everything
            groupIds.Add(MainHub.ADMIN_DATA_GROUP);

            return groupIds.ToArray();
        }

        protected async Task HandleCreateOrUpdate(
            TeamMembershipEntity teamMembershipEntity,
            string method,
            string[] modifiedProperties,
            CancellationToken cancellationToken)
        {
            var groupIds = GetGroups(teamMembershipEntity);
            var teamMembership = await _TeamMembershipService.GetAsync(teamMembershipEntity.Id, cancellationToken);
            var tasks = new List<Task>();

            foreach (var groupId in groupIds)
            {
                tasks.Add(_mainHub.Clients.Group(groupId).SendAsync(method, teamMembership, modifiedProperties, cancellationToken));
            }

            await Task.WhenAll(tasks);
        }
    }

    public class TeamMembershipCreatedSignalRHandler : TeamMembershipHandler, INotificationHandler<EntityCreated<TeamMembershipEntity>>
    {
        public TeamMembershipCreatedSignalRHandler(
            CiteContext db,
            IMapper mapper,
            ITeamMembershipService teamMembershipService,
            IHubContext<MainHub> mainHub) : base(db, mapper, teamMembershipService, mainHub) { }

        public async Task Handle(EntityCreated<TeamMembershipEntity> notification, CancellationToken cancellationToken)
        {
            await base.HandleCreateOrUpdate(notification.Entity, MainHubMethods.TeamMembershipCreated, null, cancellationToken);
        }
    }

    public class TeamMembershipUpdatedSignalRHandler : TeamMembershipHandler, INotificationHandler<EntityUpdated<TeamMembershipEntity>>
    {
        public TeamMembershipUpdatedSignalRHandler(
            CiteContext db,
            IMapper mapper,
            ITeamMembershipService teamMembershipService,
            IHubContext<MainHub> mainHub) : base(db, mapper, teamMembershipService, mainHub) { }

        public async Task Handle(EntityUpdated<TeamMembershipEntity> notification, CancellationToken cancellationToken)
        {
            await base.HandleCreateOrUpdate(
                notification.Entity,
                MainHubMethods.TeamMembershipUpdated,
                notification.ModifiedProperties.Select(x => x.TitleCaseToCamelCase()).ToArray(),
                cancellationToken);
        }
    }

    public class TeamMembershipDeletedSignalRHandler : TeamMembershipHandler, INotificationHandler<EntityDeleted<TeamMembershipEntity>>
    {
        public TeamMembershipDeletedSignalRHandler(
            CiteContext db,
            IMapper mapper,
            ITeamMembershipService teamMembershipService,
            IHubContext<MainHub> mainHub) : base(db, mapper, teamMembershipService, mainHub)
        {
        }

        public async Task Handle(EntityDeleted<TeamMembershipEntity> notification, CancellationToken cancellationToken)
        {
            var groupIds = base.GetGroups(notification.Entity);
            var tasks = new List<Task>();

            foreach (var groupId in groupIds)
            {
                tasks.Add(_mainHub.Clients.Group(groupId).SendAsync(MainHubMethods.TeamMembershipDeleted, notification.Entity, cancellationToken));
            }

            await Task.WhenAll(tasks);
        }
    }

    public class UserCreatedSubmissionHandler(ISubmissionService submissionService, CiteContext db) : INotificationHandler<EntityCreated<TeamMembershipEntity>>
    {
        public async Task Handle(EntityCreated<TeamMembershipEntity> notification, CancellationToken cancellationToken)
        {
            await submissionService.CreateUserSubmissions(notification.Entity, db, cancellationToken);
        }
    }

}