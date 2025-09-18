// Copyright 2024 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.

using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Security.Principal;
using System.Threading;
using STT = System.Threading.Tasks;
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Cite.Api.Data;
using Cite.Api.Infrastructure.Exceptions;
using SAVM = Cite.Api.ViewModels;
using Cite.Api.ViewModels;
using System.Linq;
using Cite.Api.Data.Models;

namespace Cite.Api.Services
{
    public interface ITeamMembershipService
    {
        STT.Task<TeamMembership> GetAsync(Guid id, CancellationToken ct);
        STT.Task<IEnumerable<TeamMembership>> GetByTeamAsync(Guid teamId, CancellationToken ct);
        STT.Task<TeamMembership> CreateAsync(TeamMembership teamMembership, CancellationToken ct);
        STT.Task<TeamMembership> UpdateAsync(Guid id, TeamMembership teamMembership, CancellationToken ct);
        STT.Task DeleteAsync(Guid id, CancellationToken ct);
    }

    public class TeamMembershipService : ITeamMembershipService
    {
        private readonly CiteContext _context;
        private readonly ClaimsPrincipal _user;
        private readonly IMapper _mapper;

        public TeamMembershipService(CiteContext context, IPrincipal user, IMapper mapper)
        {
            _context = context;
            _user = user as ClaimsPrincipal;
            _mapper = mapper;
        }

        public async STT.Task<TeamMembership> GetAsync(Guid id, CancellationToken ct)
        {
            var item = await _context.TeamMemberships
                .SingleOrDefaultAsync(o => o.Id == id, ct);

            if (item == null)
                throw new EntityNotFoundException<TeamMembership>();

            return _mapper.Map<SAVM.TeamMembership>(item);
        }

        public async STT.Task<IEnumerable<TeamMembership>> GetByTeamAsync(Guid teamId, CancellationToken ct)
        {
            var items = await _context.TeamMemberships
                .Where(m => m.TeamId == teamId)
                .ToListAsync(ct);

            return _mapper.Map<IEnumerable<SAVM.TeamMembership>>(items);
        }

        public async STT.Task<TeamMembership> CreateAsync(TeamMembership teamMembership, CancellationToken ct)
        {
            var teamMembershipEntity = _mapper.Map<TeamMembershipEntity>(teamMembership);

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
            teamMembershipToUpdate.RoleId = teamMembership.RoleId;
            await _context.SaveChangesAsync(ct);

            return _mapper.Map<SAVM.TeamMembership>(teamMembershipToUpdate);
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

    }
}
