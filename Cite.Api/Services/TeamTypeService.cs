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
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Cite.Api.Data;
using Cite.Api.Data.Models;
using Cite.Api.Infrastructure.Authorization;
using Cite.Api.Infrastructure.Exceptions;
using Cite.Api.Infrastructure.Extensions;
using Cite.Api.Infrastructure.QueryParameters;
using Cite.Api.ViewModels;

namespace Cite.Api.Services
{
    public interface ITeamTypeService
    {
        Task<IEnumerable<ViewModels.TeamType>> GetAsync(CancellationToken ct);
        Task<ViewModels.TeamType> GetAsync(Guid id, CancellationToken ct);
        Task<ViewModels.TeamType> CreateAsync(ViewModels.TeamType teamType, CancellationToken ct);
        Task<ViewModels.TeamType> UpdateAsync(Guid id, ViewModels.TeamType teamType, CancellationToken ct);
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

        public async Task<IEnumerable<ViewModels.TeamType>> GetAsync(CancellationToken ct)
        {
            if (!(await _authorizationService.AuthorizeAsync(_user, null, new BaseUserRequirement())).Succeeded)
                throw new ForbiddenException();

            var teamTypes = _context.TeamTypes;

            return _mapper.Map<IEnumerable<TeamType>>(await teamTypes.ToListAsync());
        }

        public async Task<ViewModels.TeamType> GetAsync(Guid id, CancellationToken ct)
        {
            if (!(await _authorizationService.AuthorizeAsync(_user, null, new BaseUserRequirement())).Succeeded)
                throw new ForbiddenException();

            var item = await _context.TeamTypes.SingleOrDefaultAsync(sm => sm.Id == id, ct);

            return _mapper.Map<TeamType>(item);
        }

        public async Task<ViewModels.TeamType> CreateAsync(ViewModels.TeamType teamType, CancellationToken ct)
        {
            if (!(await _authorizationService.AuthorizeAsync(_user, null, new ContentDeveloperRequirement())).Succeeded)
                throw new ForbiddenException();

            teamType.Id = teamType.Id != Guid.Empty ? teamType.Id : Guid.NewGuid();
            teamType.DateCreated = DateTime.UtcNow;
            teamType.CreatedBy = _user.GetId();
            teamType.DateModified = null;
            teamType.ModifiedBy = null;
            var teamTypeEntity = _mapper.Map<TeamTypeEntity>(teamType);

            _context.TeamTypes.Add(teamTypeEntity);
            await _context.SaveChangesAsync(ct);
            teamType = await GetAsync(teamTypeEntity.Id, ct);

            return teamType;
        }

        public async Task<ViewModels.TeamType> UpdateAsync(Guid id, ViewModels.TeamType teamType, CancellationToken ct)
        {
            if (!(await _authorizationService.AuthorizeAsync(_user, null, new CanIncrementMoveRequirement())).Succeeded)
                throw new ForbiddenException();

            var teamTypeToUpdate = await _context.TeamTypes.SingleOrDefaultAsync(v => v.Id == id, ct);

            if (teamTypeToUpdate == null)
                throw new EntityNotFoundException<TeamType>();

            teamType.CreatedBy = teamTypeToUpdate.CreatedBy;
            teamType.DateCreated = teamTypeToUpdate.DateCreated;
            teamType.ModifiedBy = _user.GetId();
            teamType.DateModified = DateTime.UtcNow;
            _mapper.Map(teamType, teamTypeToUpdate);

            _context.TeamTypes.Update(teamTypeToUpdate);
            await _context.SaveChangesAsync(ct);

            teamType = await GetAsync(teamTypeToUpdate.Id, ct);

            return teamType;
        }

        public async Task<bool> DeleteAsync(Guid id, CancellationToken ct)
        {
            if (!(await _authorizationService.AuthorizeAsync(_user, null, new ContentDeveloperRequirement())).Succeeded)
                throw new ForbiddenException();

            var teamTypeToDelete = await _context.TeamTypes.SingleOrDefaultAsync(v => v.Id == id, ct);

            if (teamTypeToDelete == null)
                throw new EntityNotFoundException<TeamType>();

            _context.TeamTypes.Remove(teamTypeToDelete);
            await _context.SaveChangesAsync(ct);

            return true;
        }

    }
}

