// Copyright 2022 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license, please see LICENSE.md in the project root for license information or contact permission@sei.cmu.edu for full terms.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Cite.Api.Data;
using Cite.Api.Data.Models;
using Cite.Api.Infrastructure.Extensions;
using Cite.Api.Infrastructure.Authorization;
using Cite.Api.Infrastructure.Exceptions;
using Cite.Api.ViewModels;

namespace Cite.Api.Services
{
    public interface ITeamService
    {
        Task<IEnumerable<Team>> GetAsync(CancellationToken ct);
        Task<Team> GetAsync(Guid id, CancellationToken ct);
        Task<IEnumerable<Team>> GetMineByEvaluationAsync(Guid evaluationId, CancellationToken ct);
        Task<IEnumerable<Team>> GetByUserAsync(Guid userId, CancellationToken ct);
        Task<IEnumerable<Team>> GetByTypeAsync(Guid groupId, CancellationToken ct);
        Task<IEnumerable<Team>> GetByEvaluationAsync(Guid evaluationId, CancellationToken ct);
        Task<Team> CreateAsync(Team team, CancellationToken ct);
        Task<Team> UpdateAsync(Guid id, Team team, CancellationToken ct);
        Task<bool> DeleteAsync(Guid id, CancellationToken ct);
    }

    public class TeamService : ITeamService
    {
        private readonly CiteContext _context;
        private readonly ClaimsPrincipal _user;
        private readonly IAuthorizationService _authorizationService;
        private readonly IMapper _mapper;
        private readonly ILogger<ITeamService> _logger;

        public TeamService(
            CiteContext context,
            IPrincipal team,
            IAuthorizationService authorizationService,
            ILogger<ITeamService> logger,
            IMapper mapper)
        {
            _context = context;
            _user = team as ClaimsPrincipal;
            _authorizationService = authorizationService;
            _mapper = mapper;
            _logger = logger;
        }

        public async Task<IEnumerable<Team>> GetAsync(CancellationToken ct)
        {
            var items = await _context.Teams
                .ProjectTo<Team>(_mapper.ConfigurationProvider)
                .ToArrayAsync(ct);
            return items;
        }

        public async Task<Team> GetAsync(Guid id, CancellationToken ct)
        {
            var item = await _context.Teams
                .ProjectTo<Team>(_mapper.ConfigurationProvider, dest => dest.Memberships)
                .SingleOrDefaultAsync(o => o.Id == id, ct);
            return item;
        }

        public async Task<IEnumerable<Team>> GetMineByEvaluationAsync(Guid evaluationId, CancellationToken ct)
        {
            var userId = _user.GetId();
            var items = await _context.TeamMemberships
                .Where(w => w.UserId == userId && w.Team.EvaluationId == evaluationId)
                .Include(m => m.Team)
                .ThenInclude(t => t.Memberships)
                .Select(w => w.Team)
                .ToListAsync(ct);

            return _mapper.Map<IEnumerable<Team>>(items);
        }

        public async Task<IEnumerable<Team>> GetByUserAsync(Guid userId, CancellationToken ct)
        {
            var items = await _context.TeamMemberships
                .Where(w => w.UserId == userId)
                .Select(x => x.Team)
                .ToListAsync(ct);

            return _mapper.Map<IEnumerable<Team>>(items);
        }

        public async Task<IEnumerable<Team>> GetByTypeAsync(Guid teamTypeId, CancellationToken ct)
        {
            var items = await _context.Teams
                .Where(w => w.TeamTypeId == teamTypeId)
                .ToListAsync(ct);

            return _mapper.Map<IEnumerable<Team>>(items);
        }

        public async Task<IEnumerable<Team>> GetByEvaluationAsync(Guid evaluationId, CancellationToken ct)
        {
            var items = await _context.Teams
                .Where(t => t.EvaluationId == evaluationId)
                .Include(t => t.Memberships)
                .ThenInclude(tu => tu.User)
                .ToListAsync(ct);

            return _mapper.Map<IEnumerable<Team>>(items);
        }

        public async Task<Team> CreateAsync(Team team, CancellationToken ct)
        {
            team.Id = team.Id != Guid.Empty ? team.Id : Guid.NewGuid();
            team.CreatedBy = _user.GetId();
            var teamEntity = _mapper.Map<TeamEntity>(team);

            _context.Teams.Add(teamEntity);
            await _context.SaveChangesAsync(ct);
            _logger.LogWarning($"Team {team.Name} ({teamEntity.Id}) in Evaluation {team.EvaluationId} created by {_user.GetId()}");
            return await GetAsync(teamEntity.Id, ct);
        }

        public async Task<Team> UpdateAsync(Guid id, Team team, CancellationToken ct)
        {
            var teamToUpdate = await _context.Teams.SingleOrDefaultAsync(v => v.Id == id, ct);
            if (teamToUpdate == null)
                throw new EntityNotFoundException<Team>();

            var teamTypeChanged = team.TeamTypeId != teamToUpdate.TeamTypeId;
            team.ModifiedBy = _user.GetId();
            _mapper.Map(team, teamToUpdate);
            teamToUpdate.TeamType = null;
            _context.Teams.Update(teamToUpdate);
            await _context.SaveChangesAsync(ct);
            if (teamTypeChanged)
            {
                _logger.LogWarning($"Team {teamToUpdate.Name} ({teamToUpdate.Id}) in Evaluation {team.EvaluationId} changed to TeamType {team.TeamTypeId} by {_user.GetId()}");
            }
            else
            {
                _logger.LogWarning($"Team {teamToUpdate.Name} ({teamToUpdate.Id}) in Evaluation {team.EvaluationId} updated by {_user.GetId()}");
            }
            return await GetAsync(id, ct);
        }

        public async Task<bool> DeleteAsync(Guid id, CancellationToken ct)
        {
            var teamToDelete = await _context.Teams.SingleOrDefaultAsync(v => v.Id == id, ct);
            if (teamToDelete == null)
                throw new EntityNotFoundException<Team>();

            _context.Teams.Remove(teamToDelete);
            await _context.SaveChangesAsync(ct);
            _logger.LogWarning($"Team {teamToDelete.Name} ({teamToDelete.Id}) in Evaluation {teamToDelete.EvaluationId} deleted by {_user.GetId()}");
            await DeleteTeamSubmissions((Guid)teamToDelete.EvaluationId, teamToDelete.Id, ct);
            return true;
        }

        public async Task DeleteTeamSubmissions(Guid evaluationId, Guid teamId, CancellationToken ct)
        {
            var submissions = await _context.Submissions.Where(m => m.EvaluationId == evaluationId && m.TeamId == teamId).ToListAsync(ct);
            _context.RemoveRange(submissions);
            await _context.SaveChangesAsync(ct);
        }

    }
}
