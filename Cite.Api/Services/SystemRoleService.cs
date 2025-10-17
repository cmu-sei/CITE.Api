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
    public interface ISystemRoleService
    {
        STT.Task<IEnumerable<ViewModels.SystemRole>> GetAsync(CancellationToken ct);
        STT.Task<ViewModels.SystemRole> GetAsync(Guid id, CancellationToken ct);
        STT.Task<ViewModels.SystemRole> CreateAsync(ViewModels.SystemRole systemRole, CancellationToken ct);
        STT.Task<ViewModels.SystemRole> UpdateAsync(Guid id, ViewModels.SystemRole systemRole, CancellationToken ct);
        STT.Task<bool> DeleteAsync(Guid id, CancellationToken ct);
    }

    public class SystemRoleService : ISystemRoleService
    {
        private readonly CiteContext _context;
        private readonly ClaimsPrincipal _user;
        private readonly IMapper _mapper;

        public SystemRoleService(CiteContext context, IPrincipal user, IMapper mapper)
        {
            _context = context;
            _user = user as ClaimsPrincipal;
            _mapper = mapper;
        }

        public async STT.Task<IEnumerable<ViewModels.SystemRole>> GetAsync(CancellationToken ct)
        {
            var items = await _context.SystemRoles
                .ToListAsync(ct);

            return _mapper.Map<IEnumerable<SAVM.SystemRole>>(items);
        }

        public async STT.Task<ViewModels.SystemRole> GetAsync(Guid id, CancellationToken ct)
        {
            var item = await _context.SystemRoles
                .SingleOrDefaultAsync(o => o.Id == id, ct);

            return _mapper.Map<SAVM.SystemRole>(item);
        }

        public async STT.Task<ViewModels.SystemRole> CreateAsync(ViewModels.SystemRole systemRole, CancellationToken ct)
        {
            var systemRoleEntity = _mapper.Map<SystemRoleEntity>(systemRole);

            _context.SystemRoles.Add(systemRoleEntity);
            await _context.SaveChangesAsync(ct);

            return await GetAsync(systemRoleEntity.Id, ct);
        }

        public async STT.Task<ViewModels.SystemRole> UpdateAsync(Guid id, ViewModels.SystemRole systemRole, CancellationToken ct)
        {
            var systemRoleToUpdate = await _context.SystemRoles.SingleOrDefaultAsync(v => v.Id == id, ct);

            if (systemRoleToUpdate == null)
                throw new EntityNotFoundException<SAVM.SystemRole>();

            _mapper.Map(systemRole, systemRoleToUpdate);

            _context.SystemRoles.Update(systemRoleToUpdate);
            await _context.SaveChangesAsync(ct);

            return _mapper.Map(systemRoleToUpdate, systemRole);
        }

        public async STT.Task<bool> DeleteAsync(Guid id, CancellationToken ct)
        {
            var systemRoleToDelete = await _context.SystemRoles.SingleOrDefaultAsync(v => v.Id == id, ct);

            if (systemRoleToDelete == null)
                throw new EntityNotFoundException<SAVM.SystemRole>();

            _context.SystemRoles.Remove(systemRoleToDelete);
            await _context.SaveChangesAsync(ct);

            return true;
        }

    }
}
