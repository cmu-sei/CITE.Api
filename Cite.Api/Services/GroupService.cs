// Copyright 2024 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Security.Principal;
using System.Threading;
using STT = System.Threading.Tasks;
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Cite.Api.Data;
using Cite.Api.Data.Models;
using Cite.Api.Infrastructure.Exceptions;
using SAVM = Cite.Api.ViewModels;

namespace Cite.Api.Services
{
    public interface IGroupService
    {
        STT.Task<IEnumerable<ViewModels.Group>> GetAsync(CancellationToken ct);
        STT.Task<ViewModels.Group> GetAsync(Guid id, CancellationToken ct);
        STT.Task<ViewModels.Group> CreateAsync(ViewModels.Group group, CancellationToken ct);
        STT.Task<ViewModels.Group> UpdateAsync(Guid id, ViewModels.Group groupForm, CancellationToken ct);
        STT.Task DeleteAsync(Guid Id, CancellationToken ct);
        STT.Task<ViewModels.GroupMembership> GetMembershipAsync(Guid id, CancellationToken ct);
        STT.Task<IEnumerable<ViewModels.GroupMembership>> GetMembershipsForGroupAsync(Guid groupId, CancellationToken ct);
        STT.Task<ViewModels.GroupMembership> CreateMembershipAsync(ViewModels.GroupMembership groupMembership, CancellationToken ct);
        STT.Task DeleteMembershipAsync(Guid id, CancellationToken ct);
    }

    public class GroupService : IGroupService
    {
        private readonly CiteContext _context;
        private readonly ClaimsPrincipal _user;
        private readonly IMapper _mapper;
        private readonly ITaskService _taskService;
        private readonly IStackStormService _stackstormService;

        public GroupService(CiteContext context,
                                IPrincipal user,
                                IMapper mapper,
                                ITaskService taskService,
                                IStackStormService stackstormService)
        {
            _context = context;
            _user = user as ClaimsPrincipal;
            _mapper = mapper;
            _taskService = taskService;
            _stackstormService = stackstormService;
        }

        public async STT.Task<IEnumerable<ViewModels.Group>> GetAsync(CancellationToken ct)
        {
            var items = await _context.Groups.ToListAsync(ct);

            return _mapper.Map<IEnumerable<SAVM.Group>>(items);
        }

        public async STT.Task<ViewModels.Group> GetAsync(Guid id, CancellationToken ct)
        {
            var item = await _context.Groups.SingleOrDefaultAsync(o => o.Id == id, ct);

            return _mapper.Map<SAVM.Group>(item);
        }

        public async STT.Task<ViewModels.Group> CreateAsync(ViewModels.Group group, CancellationToken ct)
        {
            var groupEntity = _mapper.Map<GroupEntity>(group);
            _context.Groups.Add(groupEntity);
            await _context.SaveChangesAsync(ct);
            group = await GetAsync(groupEntity.Id, ct);

            return group;
        }

        public async STT.Task<SAVM.Group> UpdateAsync(Guid id, ViewModels.Group group, CancellationToken ct)
        {
            var groupToUpdate = await _context.Groups.SingleOrDefaultAsync(v => v.Id == id, ct);
            if (groupToUpdate == null)
                throw new EntityNotFoundException<SAVM.Group>();

            _mapper.Map(group, groupToUpdate);
            await _context.SaveChangesAsync(ct);
            var updatedGroup = _mapper.Map<SAVM.Group>(groupToUpdate);

            return updatedGroup;
        }

        public async STT.Task DeleteAsync(Guid id, CancellationToken ct)
        {
            var groupToDelete = await _context.Groups.SingleOrDefaultAsync(v => v.Id == id, ct);
            if (groupToDelete == null)
                throw new EntityNotFoundException<SAVM.Group>();

            _context.Groups.Remove(groupToDelete);
            await _context.SaveChangesAsync(ct);
        }

        public async STT.Task<IEnumerable<ViewModels.GroupMembership>> GetMembershipsForGroupAsync(Guid groupId, CancellationToken ct)
        {
            var items = await _context.GroupMemberships.Where(m => m.GroupId == groupId).ToListAsync(ct);

            return _mapper.Map<IEnumerable<SAVM.GroupMembership>>(items);
        }

        public async STT.Task<ViewModels.GroupMembership> GetMembershipAsync(Guid id, CancellationToken ct)
        {
            var item = await _context.GroupMemberships.SingleOrDefaultAsync(o => o.Id == id, ct);

            return _mapper.Map<SAVM.GroupMembership>(item);
        }

        public async STT.Task<ViewModels.GroupMembership> CreateMembershipAsync(ViewModels.GroupMembership groupMembership, CancellationToken ct)
        {
            var groupMembershipEntity = _mapper.Map<GroupMembershipEntity>(groupMembership);
            _context.GroupMemberships.Add(groupMembershipEntity);
            await _context.SaveChangesAsync(ct);
            groupMembership = await GetMembershipAsync(groupMembershipEntity.Id, ct);

            return groupMembership;
        }

        public async STT.Task DeleteMembershipAsync(Guid id, CancellationToken ct)
        {
            var groupMembershipToDelete = await _context.GroupMemberships.SingleOrDefaultAsync(v => v.Id == id, ct);
            if (groupMembershipToDelete == null)
                throw new EntityNotFoundException<SAVM.Group>();

            _context.GroupMemberships.Remove(groupMembershipToDelete);
            await _context.SaveChangesAsync(ct);
        }

    }
}
