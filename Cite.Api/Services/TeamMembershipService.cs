// Copyright 2024 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.

using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Security.Principal;
using System.Threading;
using STT = System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Cite.Api.Data;
using Cite.Api.Data.Enumerations;
using Cite.Api.Infrastructure.Exceptions;
using SAVM = Cite.Api.ViewModels;
using Cite.Api.ViewModels;
using System.Linq;
using Cite.Api.Data.Models;
using Cite.Api.Infrastructure.Mappings;

namespace Cite.Api.Services
{
    public interface ITeamMembershipService
    {
        STT.Task<TeamMembership> GetAsync(Guid id, CancellationToken ct);
        STT.Task<IEnumerable<TeamMembership>> GetByTeamAsync(Guid teamId, CancellationToken ct);
        STT.Task<TeamMembership> CreateAsync(TeamMembership teamMembership, CancellationToken ct);
        STT.Task<TeamMembership> UpdateAsync(Guid id, TeamMembership teamMembership, CancellationToken ct);
        STT.Task DeleteAsync(Guid id, CancellationToken ct);
        STT.Task<bool> RoleGrantsCurrentOfficialScoreAsync(Guid roleId, CancellationToken ct);
    }

    public class TeamMembershipService : ITeamMembershipService
    {
        private readonly CiteContext _context;
        private readonly ClaimsPrincipal _user;

        public TeamMembershipService(CiteContext context, IPrincipal user)
        {
            _context = context;
            _user = user as ClaimsPrincipal;
        }

        public async STT.Task<TeamMembership> GetAsync(Guid id, CancellationToken ct)
        {
            var item = await _context.TeamMemberships
                .SingleOrDefaultAsync(o => o.Id == id, ct);

            if (item == null)
                throw new EntityNotFoundException<TeamMembership>();

            return item.ToTeamMembership();
        }

        public async STT.Task<IEnumerable<TeamMembership>> GetByTeamAsync(Guid teamId, CancellationToken ct)
        {
            var items = await _context.TeamMemberships
                .Where(m => m.TeamId == teamId)
                .ToListAsync(ct);

            return items.Select(x => x.ToTeamMembership()).ToList();
        }

        public async STT.Task<TeamMembership> CreateAsync(TeamMembership teamMembership, CancellationToken ct)
        {
            var teamMembershipEntity = teamMembership.ToEntity();

            _context.TeamMemberships.Add(teamMembershipEntity);
            await _context.SaveChangesAsync(ct);
            var team = await GetAsync(teamMembershipEntity.Id, ct);

            return team;
        }
        public async STT.Task<TeamMembership> UpdateAsync(Guid id, TeamMembership teamMembership, CancellationToken ct)
        {
            var teamMembershipToUpdate = await _context.TeamMemberships.SingleOrDefaultAsync(v => v.Id == id, ct);
            if (teamMembershipToUpdate == null)
                throw new EntityNotFoundException<SAVM.Team>();

            teamMembershipToUpdate.Role = null;
            // a request without a role leaves the member with the default Member role
            teamMembershipToUpdate.RoleId = teamMembership.RoleId == Guid.Empty ? TeamRoleDefaults.TeamMemberRoleId : teamMembership.RoleId;
            await _context.SaveChangesAsync(ct);

            return teamMembershipToUpdate.ToTeamMembership();
        }
        public async STT.Task DeleteAsync(Guid id, CancellationToken ct)
        {
            var teamMembershipToDelete = await _context.TeamMemberships.SingleOrDefaultAsync(v => v.Id == id, ct);

            if (teamMembershipToDelete == null)
                throw new EntityNotFoundException<SAVM.TeamMembership>();

            _context.TeamMemberships.Remove(teamMembershipToDelete);
            await _context.SaveChangesAsync(ct);

            return;
        }

        /// <summary>
        /// Whether the team role reads or scores the current move's official score. An empty role id is the
        /// Member role, which the membership is given when no role is named.
        /// </summary>
        public async STT.Task<bool> RoleGrantsCurrentOfficialScoreAsync(Guid roleId, CancellationToken ct)
        {
            if (roleId == Guid.Empty)
                roleId = TeamRoleDefaults.TeamMemberRoleId;

            var permissions = await _context.TeamRoles
                .Where(r => r.Id == roleId)
                .Select(r => r.Permissions)
                .SingleOrDefaultAsync(ct);

            return permissions != null &&
                (permissions.Contains(TeamPermission.ViewCurrentOfficialScore) || permissions.Contains(TeamPermission.EditOfficialScore));
        }

    }
}
