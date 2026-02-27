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
using Microsoft.Extensions.Logging;

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
        private readonly ILogger<ITeamMembershipService> _logger;

        public TeamMembershipService(CiteContext context, IPrincipal user, IMapper mapper, ILogger<ITeamMembershipService> logger)
        {
            _context = context;
            _user = user as ClaimsPrincipal;
            _mapper = mapper;
            _logger = logger;
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
            // Validate required fields
            if (teamMembership.TeamId == Guid.Empty)
                throw new ArgumentException("TeamId is required");

            if (teamMembership.UserId == Guid.Empty)
                throw new ArgumentException("UserId is required");

            // Validate that the team exists
            var team = await _context.Teams
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == teamMembership.TeamId, ct);

            if (team == null)
                throw new EntityNotFoundException<Team>($"Team {teamMembership.TeamId} not found");

            // Validate that the user exists
            var userExists = await _context.Users
                .AnyAsync(u => u.Id == teamMembership.UserId, ct);

            if (!userExists)
                throw new EntityNotFoundException<User>($"User {teamMembership.UserId} not found");

            // Check for duplicate membership
            var existingMembership = await _context.TeamMemberships
                .FirstOrDefaultAsync(tm => tm.TeamId == teamMembership.TeamId && tm.UserId == teamMembership.UserId, ct);

            if (existingMembership != null)
            {
                _logger.LogWarning($"User {teamMembership.UserId} is already a member of team {teamMembership.TeamId}");
                throw new InvalidOperationException($"User is already a member of this team");
            }

            _logger.LogInformation($"Adding user {teamMembership.UserId} to team {teamMembership.TeamId} ({team.Name}) in evaluation {team.EvaluationId}");

            var teamMembershipEntity = _mapper.Map<TeamMembershipEntity>(teamMembership);

            _context.TeamMemberships.Add(teamMembershipEntity);
            await _context.SaveChangesAsync(ct);

            _logger.LogInformation($"Successfully added user {teamMembership.UserId} to team {teamMembership.TeamId}");

            var result = await GetAsync(teamMembershipEntity.Id, ct);
            return result;
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
