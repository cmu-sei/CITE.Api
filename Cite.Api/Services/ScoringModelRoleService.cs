// Copyright 2024 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.

using System.Linq;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Security.Principal;
using System.Threading;
using STT = System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Cite.Api.Data;
using Cite.Api.Infrastructure.Exceptions;
using SAVM = Cite.Api.ViewModels;
using Cite.Api.ViewModels;
using Cite.Api.Infrastructure.Mappings;

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

        public ScoringModelRoleService(CiteContext context, IPrincipal user)
        {
            _context = context;
            _user = user as ClaimsPrincipal;
        }

        public async STT.Task<IEnumerable<ViewModels.ScoringModelRole>> GetAsync(CancellationToken ct)
        {
            var items = await _context.ScoringModelRoles
                .ToListAsync(ct);

            return items.Select(x => x.ToScoringModelRole()).ToList();
        }

        public async STT.Task<ViewModels.ScoringModelRole> GetAsync(Guid id, CancellationToken ct)
        {
            var item = await _context.ScoringModelRoles
                .SingleOrDefaultAsync(o => o.Id == id, ct);

            if (item == null)
                throw new EntityNotFoundException<ScoringModelRole>();

            return item.ToScoringModelRole();
        }

    }
}
