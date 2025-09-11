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

namespace Cite.Api.Services
{
    public interface IScoringModelRoleService
    {
        STT.Task<IEnumerable<ViewModels.ScoringModelRole>> GetAsync(CancellationToken ct);
        STT.Task<ViewModels.ScoringModelRole> GetAsync(Guid id, CancellationToken ct);
    }

    public class ScoringModelRoleService : IScoringModelRoleService
    {
        private readonly CiteContext _context;
        private readonly ClaimsPrincipal _user;
        private readonly IMapper _mapper;

        public ScoringModelRoleService(CiteContext context, IPrincipal user, IMapper mapper)
        {
            _context = context;
            _user = user as ClaimsPrincipal;
            _mapper = mapper;
        }

        public async STT.Task<IEnumerable<ViewModels.ScoringModelRole>> GetAsync(CancellationToken ct)
        {
            var items = await _context.ScoringModelRoles
                .ToListAsync(ct);

            return _mapper.Map<IEnumerable<SAVM.ScoringModelRole>>(items);
        }

        public async STT.Task<ViewModels.ScoringModelRole> GetAsync(Guid id, CancellationToken ct)
        {
            var item = await _context.ScoringModelRoles
                .SingleOrDefaultAsync(o => o.Id == id, ct);

            if (item == null)
                throw new EntityNotFoundException<ScoringModelRole>();

            return _mapper.Map<SAVM.ScoringModelRole>(item);
        }

    }
}
