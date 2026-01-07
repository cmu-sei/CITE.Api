// Copyright 2022 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license, please see LICENSE.md in the project root for license information or contact permission@sei.cmu.edu for full terms.

using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Cite.Api.Data;
using Cite.Api.Data.Models;
using Cite.Api.Infrastructure.Exceptions;
using Cite.Api.Infrastructure.Extensions;
using Cite.Api.ViewModels;

namespace Cite.Api.Services
{
    public interface ITeamTypeService
    {
        Task<IEnumerable<TeamType>> GetAsync(CancellationToken ct);
        Task<TeamType> GetAsync(Guid id, CancellationToken ct);
        Task<TeamType> CreateAsync(TeamType teamType, CancellationToken ct);
        Task<TeamType> InternalCreateAsync(TeamType teamType, CancellationToken ct);
        Task<TeamType> UpdateAsync(Guid id, TeamType teamType, CancellationToken ct);
        Task<bool> DeleteAsync(Guid id, CancellationToken ct);
    }

    public class TeamTypeService : ITeamTypeService
    {
        private readonly CiteContext _context;
        private readonly IAuthorizationService _authorizationService;
        private readonly ClaimsPrincipal _user;
        private readonly IMapper _mapper;

        public TeamTypeService(
            CiteContext context,
            IAuthorizationService authorizationService,
            IPrincipal user,
            IMapper mapper)
        {
            _context = context;
            _authorizationService = authorizationService;
            _user = user as ClaimsPrincipal;
            _mapper = mapper;
        }

        public async Task<IEnumerable<TeamType>> GetAsync(CancellationToken ct)
        {
            var teamTypes = _context.TeamTypes;

            return _mapper.Map<IEnumerable<TeamType>>(await teamTypes.ToListAsync());
        }

        public async Task<TeamType> GetAsync(Guid id, CancellationToken ct)
        {
            var item = await _context.TeamTypes.SingleOrDefaultAsync(sm => sm.Id == id, ct);

            return _mapper.Map<TeamType>(item);
        }

        public async Task<TeamType> CreateAsync(TeamType teamType, CancellationToken ct)
        {
            return await InternalCreateAsync(teamType, ct);
        }

        public async Task<TeamType> InternalCreateAsync(TeamType teamType, CancellationToken ct)
        {
            teamType.Id = teamType.Id != Guid.Empty ? teamType.Id : Guid.NewGuid();
            teamType.CreatedBy = _user.GetId();
            var teamTypeEntity = _mapper.Map<TeamTypeEntity>(teamType);
            _context.TeamTypes.Add(teamTypeEntity);
            await _context.SaveChangesAsync(ct);
            teamType = await GetAsync(teamTypeEntity.Id, ct);

            return teamType;
        }

        public async Task<TeamType> UpdateAsync(Guid id, TeamType teamType, CancellationToken ct)
        {
            var teamTypeToUpdate = await _context.TeamTypes.SingleOrDefaultAsync(v => v.Id == id, ct);
            if (teamTypeToUpdate == null)
                throw new EntityNotFoundException<TeamType>();

            teamType.ModifiedBy = _user.GetId();
            _mapper.Map(teamType, teamTypeToUpdate);
            _context.TeamTypes.Update(teamTypeToUpdate);
            await _context.SaveChangesAsync(ct);
            teamType = await GetAsync(teamTypeToUpdate.Id, ct);

            return teamType;
        }

        public async Task<bool> DeleteAsync(Guid id, CancellationToken ct)
        {
            var teamTypeToDelete = await _context.TeamTypes.SingleOrDefaultAsync(v => v.Id == id, ct);
            if (teamTypeToDelete == null)
                throw new EntityNotFoundException<TeamType>();

            _context.TeamTypes.Remove(teamTypeToDelete);
            await _context.SaveChangesAsync(ct);

            return true;
        }

    }
}
