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
using Cite.Api.Infrastructure.Authorization;
using Cite.Api.Infrastructure.Exceptions;
using Cite.Api.Infrastructure.Extensions;
using Cite.Api.ViewModels;

namespace Cite.Api.Services
{
    public interface IGroupTeamService
    {
        Task<IEnumerable<ViewModels.GroupTeam>> GetAsync(CancellationToken ct);
        Task<ViewModels.GroupTeam> GetAsync(Guid id, CancellationToken ct);
        Task<ViewModels.GroupTeam> CreateAsync(ViewModels.GroupTeam groupTeam, CancellationToken ct);
        Task<bool> DeleteAsync(Guid id, CancellationToken ct);
        Task<bool> DeleteByIdsAsync(Guid teamId, Guid groupId, CancellationToken ct);
    }

    public class GroupTeamService : IGroupTeamService
    {
        private readonly CiteContext _context;
        private readonly IAuthorizationService _authorizationService;
        private readonly ClaimsPrincipal _user;
        private readonly IMapper _mapper;

        public GroupTeamService(CiteContext context, IAuthorizationService authorizationService, IPrincipal user, IMapper mapper)
        {
            _context = context;
            _authorizationService = authorizationService;
            _user = user as ClaimsPrincipal;
            _mapper = mapper;
        }

        public async Task<IEnumerable<ViewModels.GroupTeam>> GetAsync(CancellationToken ct)
        {
            if (!(await _authorizationService.AuthorizeAsync(_user, null, new FullRightsRequirement())).Succeeded)
                throw new ForbiddenException();

            var items = await _context.GroupTeams
                .ToListAsync(ct);

            return _mapper.Map<IEnumerable<GroupTeam>>(items);
        }

        public async Task<ViewModels.GroupTeam> GetAsync(Guid id, CancellationToken ct)
        {
            if (!(await _authorizationService.AuthorizeAsync(_user, null, new FullRightsRequirement())).Succeeded)
                throw new ForbiddenException();

            var item = await _context.GroupTeams
                .SingleOrDefaultAsync(o => o.Id == id, ct);

            return _mapper.Map<GroupTeam>(item);
        }

        public async Task<ViewModels.GroupTeam> CreateAsync(ViewModels.GroupTeam groupTeam, CancellationToken ct)
        {
            if (!(await _authorizationService.AuthorizeAsync(_user, null, new FullRightsRequirement())).Succeeded)
                throw new ForbiddenException();

            groupTeam.Id = groupTeam.Id != Guid.Empty ? groupTeam.Id : Guid.NewGuid();
            groupTeam.DateCreated = DateTime.UtcNow;
            groupTeam.CreatedBy = _user.GetId();
            var groupTeamEntity = _mapper.Map<GroupTeamEntity>(groupTeam);

            _context.GroupTeams.Add(groupTeamEntity);
            await _context.SaveChangesAsync(ct);

            return await GetAsync(groupTeamEntity.Id, ct);
        }

        public async Task<bool> DeleteAsync(Guid id, CancellationToken ct)
        {
            if (!(await _authorizationService.AuthorizeAsync(_user, null, new FullRightsRequirement())).Succeeded)
                throw new ForbiddenException();

            var groupTeamToDelete = await _context.GroupTeams.SingleOrDefaultAsync(v => v.Id == id, ct);

            if (groupTeamToDelete == null)
                throw new EntityNotFoundException<GroupTeam>();

            _context.GroupTeams.Remove(groupTeamToDelete);
            await _context.SaveChangesAsync(ct);

            return true;
        }

        public async Task<bool> DeleteByIdsAsync(Guid teamId, Guid groupId, CancellationToken ct)
        {
            if (!(await _authorizationService.AuthorizeAsync(_user, null, new FullRightsRequirement())).Succeeded)
                throw new ForbiddenException();

            var groupTeamToDelete = await _context.GroupTeams.SingleOrDefaultAsync(v => (v.GroupId == groupId) && (v.TeamId == teamId), ct);

            if (groupTeamToDelete == null)
                throw new EntityNotFoundException<GroupTeam>();

            _context.GroupTeams.Remove(groupTeamToDelete);
            await _context.SaveChangesAsync(ct);

            return true;
        }

    }
}

