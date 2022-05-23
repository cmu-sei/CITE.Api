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
using Cite.Api.Data;
using Cite.Api.Data.Models;
using Cite.Api.Infrastructure.Extensions;
using Cite.Api.Infrastructure.Authorization;
using Cite.Api.Infrastructure.Exceptions;
using Cite.Api.ViewModels;

namespace Cite.Api.Services
{
    public interface IGroupService
    {
        Task<IEnumerable<ViewModels.Group>> GetAsync(CancellationToken ct);
        Task<ViewModels.Group> GetAsync(Guid id, CancellationToken ct);
        Task<IEnumerable<ViewModels.Group>> GetMineAsync(CancellationToken ct);
        Task<IEnumerable<ViewModels.Group>> GetByTeamAsync(Guid id, CancellationToken ct);
        Task<ViewModels.Group> CreateAsync(ViewModels.Group group, CancellationToken ct);
        Task<ViewModels.Group> UpdateAsync(Guid id, ViewModels.Group group, CancellationToken ct);
        Task<bool> DeleteAsync(Guid id, CancellationToken ct);
    }

    public class GroupService : IGroupService
    {
        private readonly CiteContext _context;
        private readonly ClaimsPrincipal _user;
        private readonly IAuthorizationService _authorizationService;
        private readonly IMapper _mapper;

        public GroupService(
            CiteContext context,
            IPrincipal user,
            IAuthorizationService authorizationService,
            IMapper mapper)
        {
            _context = context;
            _user = user as ClaimsPrincipal;
            _authorizationService = authorizationService;
            _mapper = mapper;
        }

        public async Task<IEnumerable<ViewModels.Group>> GetAsync(CancellationToken ct)
        {
            if(!(await _authorizationService.AuthorizeAsync(_user, null, new FullRightsRequirement())).Succeeded)
                throw new ForbiddenException();

            var items = await _context.Groups
                .ProjectTo<ViewModels.Group>(_mapper.ConfigurationProvider)
                .ToArrayAsync(ct);
            return items;
        }

        public async Task<ViewModels.Group> GetAsync(Guid id, CancellationToken ct)
        {
            if (!(await _authorizationService.AuthorizeAsync(_user, null, new FullRightsRequirement())).Succeeded)
                throw new ForbiddenException();

            var item = await _context.Groups
                .ProjectTo<ViewModels.Group>(_mapper.ConfigurationProvider, dest => dest.Teams)
                .SingleOrDefaultAsync(o => o.Id == id, ct);
            return item;
        }

        public async Task<IEnumerable<ViewModels.Group>> GetMineAsync(CancellationToken ct)
        {
            if(!(await _authorizationService.AuthorizeAsync(_user, null, new BaseUserRequirement())).Succeeded)
                throw new ForbiddenException();

            var teamIdList = await _context.TeamUsers
                .Where(w => w.UserId == _user.GetId())
                .Select(x => x.Team.Id)
                .ToListAsync(ct);
            var items = await _context.GroupTeams
                .Where(x => teamIdList.Contains(x.TeamId))
                .Select(y => y.Group)
                .ProjectTo<ViewModels.Group>(_mapper.ConfigurationProvider, dest => dest.Teams)
                .ToListAsync(ct);

            return items;
        }

        public async Task<IEnumerable<ViewModels.Group>> GetByTeamAsync(Guid teamId, CancellationToken ct)
        {
            if (!(await _authorizationService.AuthorizeAsync(_user, null, new FullRightsRequirement())).Succeeded)
                throw new ForbiddenException();

            var items = await _context.GroupTeams
                .Where(w => w.TeamId == teamId)
                .Select(x => x.Group)
                .ProjectTo<ViewModels.Group>(_mapper.ConfigurationProvider, dest => dest.Teams)
                .ToListAsync(ct);

            return items;
        }

        public async Task<ViewModels.Group> CreateAsync(ViewModels.Group group, CancellationToken ct)
        {
            if (!(await _authorizationService.AuthorizeAsync(_user, null, new FullRightsRequirement())).Succeeded)
                throw new ForbiddenException();

            group.Id = group.Id != Guid.Empty ? group.Id : Guid.NewGuid();
            group.DateCreated = DateTime.UtcNow;
            group.CreatedBy = _user.GetId();
            group.DateModified = null;
            group.ModifiedBy = null;
            var groupEntity = _mapper.Map<GroupEntity>(group);

            _context.Groups.Add(groupEntity);
            await _context.SaveChangesAsync(ct);

            return await GetAsync(groupEntity.Id, ct);
        }

        public async Task<ViewModels.Group> UpdateAsync(Guid id, ViewModels.Group group, CancellationToken ct)
        {
            if (!(await _authorizationService.AuthorizeAsync(_user, null, new FullRightsRequirement())).Succeeded)
                throw new ForbiddenException();

            // Don't allow changing your own Id
            if (id == _user.GetId() && id != group.Id)
            {
                throw new ForbiddenException("You cannot change your own Id");
            }

            var groupToUpdate = await _context.Groups.SingleOrDefaultAsync(v => v.Id == id, ct);

            if (groupToUpdate == null)
                throw new EntityNotFoundException<Group>();

            group.CreatedBy = groupToUpdate.CreatedBy;
            group.DateCreated = groupToUpdate.DateCreated;
            group.ModifiedBy = _user.GetId();
            group.DateModified = DateTime.UtcNow;
            _mapper.Map(group, groupToUpdate);

            _context.Groups.Update(groupToUpdate);
            await _context.SaveChangesAsync(ct);

            return await GetAsync(id, ct);
        }

        public async Task<bool> DeleteAsync(Guid id, CancellationToken ct)
        {
            if (!(await _authorizationService.AuthorizeAsync(_user, null, new FullRightsRequirement())).Succeeded)
                throw new ForbiddenException();

            if (id == _user.GetId())
            {
                throw new ForbiddenException("You cannot delete your own account");
            }

            var groupToDelete = await _context.Groups.SingleOrDefaultAsync(v => v.Id == id, ct);

            if (groupToDelete == null)
                throw new EntityNotFoundException<Group>();

            _context.Groups.Remove(groupToDelete);
            await _context.SaveChangesAsync(ct);

            return true;
        }

    }
}

