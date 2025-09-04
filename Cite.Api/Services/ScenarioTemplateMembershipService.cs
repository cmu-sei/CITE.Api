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

namespace Cite.Api.Services
{
    public interface IScoringModelMembershipService
    {
        STT.Task<ViewModels.ScoringModelMembership> GetAsync(Guid id, CancellationToken ct);
        STT.Task<IEnumerable<ViewModels.ScoringModelMembership>> GetByScoringModelAsync(Guid scoringModelId, CancellationToken ct);
        STT.Task<ViewModels.ScoringModelMembership> CreateAsync(ViewModels.ScoringModelMembership scoringModelMembership, CancellationToken ct);
        STT.Task<ViewModels.ScoringModelMembership> UpdateAsync(Guid id, ViewModels.ScoringModelMembership scoringModelMembership, CancellationToken ct);
        STT.Task DeleteAsync(Guid id, CancellationToken ct);
    }

    public class ScoringModelMembershipService : IScoringModelMembershipService
    {
        private readonly CiteContext _context;
        private readonly ClaimsPrincipal _user;
        private readonly IMapper _mapper;

        public ScoringModelMembershipService(CiteContext context, IPrincipal user, IMapper mapper)
        {
            _context = context;
            _user = user as ClaimsPrincipal;
            _mapper = mapper;
        }

        public async STT.Task<ViewModels.ScoringModelMembership> GetAsync(Guid id, CancellationToken ct)
        {
            var item = await _context.ScoringModelMemberships
                .SingleOrDefaultAsync(o => o.Id == id, ct);

            if (item == null)
                throw new EntityNotFoundException<ScoringModelMembership>();

            return _mapper.Map<SAVM.ScoringModelMembership>(item);
        }

        public async STT.Task<IEnumerable<ViewModels.ScoringModelMembership>> GetByScoringModelAsync(Guid scoringModelId, CancellationToken ct)
        {
            var items = await _context.ScoringModelMemberships
                .Where(m => m.ScoringModelId == scoringModelId)
                .ToListAsync(ct);

            return _mapper.Map<IEnumerable<SAVM.ScoringModelMembership>>(items);
        }

        public async STT.Task<ViewModels.ScoringModelMembership> CreateAsync(ViewModels.ScoringModelMembership scoringModelMembership, CancellationToken ct)
        {
            var scoringModelMembershipEntity = _mapper.Map<ScoringModelMembershipEntity>(scoringModelMembership);

            _context.ScoringModelMemberships.Add(scoringModelMembershipEntity);
            await _context.SaveChangesAsync(ct);
            var evaluation = await GetAsync(scoringModelMembershipEntity.Id, ct);

            return evaluation;
        }
        public async STT.Task<ViewModels.ScoringModelMembership> UpdateAsync(Guid id, ViewModels.ScoringModelMembership scoringModelMembership, CancellationToken ct)
        {
            var scoringModelMembershipToUpdate = await _context.ScoringModelMemberships.SingleOrDefaultAsync(v => v.Id == id, ct);
            if (scoringModelMembershipToUpdate == null)
                throw new EntityNotFoundException<SAVM.Evaluation>();

            scoringModelMembershipToUpdate.Role = null;
            scoringModelMembershipToUpdate.RoleId = scoringModelMembership.RoleId;
            await _context.SaveChangesAsync(ct);

            return _mapper.Map<SAVM.ScoringModelMembership>(scoringModelMembershipToUpdate);
        }
        public async STT.Task DeleteAsync(Guid id, CancellationToken ct)
        {
            var scoringModelMembershipToDelete = await _context.ScoringModelMemberships.SingleOrDefaultAsync(v => v.Id == id, ct);

            if (scoringModelMembershipToDelete == null)
                throw new EntityNotFoundException<SAVM.ScoringModelMembership>();

            _context.ScoringModelMemberships.Remove(scoringModelMembershipToDelete);
            await _context.SaveChangesAsync(ct);

            return;
        }

    }
}
