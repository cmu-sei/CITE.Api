// Copyright 2025 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.

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
using System.Linq;
using Cite.Api.Data.Models;
using Cite.Api.Infrastructure.Mappings;

namespace Cite.Api.Services
{
    public interface IEvaluationMembershipService
    {
        STT.Task<ViewModels.EvaluationMembership> GetAsync(Guid id, CancellationToken ct);
        STT.Task<IEnumerable<ViewModels.EvaluationMembership>> GetByEvaluationAsync(Guid evaluationId, CancellationToken ct);
        STT.Task<ViewModels.EvaluationMembership> CreateAsync(ViewModels.EvaluationMembership evaluationMembership, CancellationToken ct);
        STT.Task<ViewModels.EvaluationMembership> UpdateAsync(Guid id, ViewModels.UpdateEvaluationMembershipRequest evaluationMembership, CancellationToken ct);
        STT.Task DeleteAsync(Guid id, CancellationToken ct);
    }

    public class EvaluationMembershipService : IEvaluationMembershipService
    {
        private readonly CiteContext _context;
        private readonly ClaimsPrincipal _user;

        public EvaluationMembershipService(CiteContext context, IPrincipal user)
        {
            _context = context;
            _user = user as ClaimsPrincipal;
        }

        public async STT.Task<ViewModels.EvaluationMembership> GetAsync(Guid id, CancellationToken ct)
        {
            var item = await _context.EvaluationMemberships
                .SingleOrDefaultAsync(o => o.Id == id, ct);

            if (item == null)
                throw new EntityNotFoundException<EvaluationMembership>();

            return item.ToEvaluationMembership();
        }

        public async STT.Task<IEnumerable<ViewModels.EvaluationMembership>> GetByEvaluationAsync(Guid evaluationId, CancellationToken ct)
        {
            var items = await _context.EvaluationMemberships
                .Where(m => m.EvaluationId == evaluationId)
                .ToListAsync(ct);

            return items.Select(x => x.ToEvaluationMembership()).ToList();
        }

        public async STT.Task<ViewModels.EvaluationMembership> CreateAsync(ViewModels.EvaluationMembership evaluationMembership, CancellationToken ct)
        {
            var evaluationMembershipEntity = evaluationMembership.ToEntity();

            _context.EvaluationMemberships.Add(evaluationMembershipEntity);
            await _context.SaveChangesAsync(ct);
            var evaluation = await GetAsync(evaluationMembershipEntity.Id, ct);

            return evaluation;
        }
        public async STT.Task<ViewModels.EvaluationMembership> UpdateAsync(Guid id, ViewModels.UpdateEvaluationMembershipRequest evaluationMembership, CancellationToken ct)
        {
            var evaluationMembershipToUpdate = await _context.EvaluationMemberships.SingleOrDefaultAsync(v => v.Id == id, ct);
            if (evaluationMembershipToUpdate == null)
                throw new EntityNotFoundException<SAVM.EvaluationMembership>();

            evaluationMembershipToUpdate.Role = null;
            evaluationMembershipToUpdate.RoleId = evaluationMembership.RoleId;
            await _context.SaveChangesAsync(ct);

            return evaluationMembershipToUpdate.ToEvaluationMembership();
        }
        public async STT.Task DeleteAsync(Guid id, CancellationToken ct)
        {
            var evaluationMembershipToDelete = await _context.EvaluationMemberships.SingleOrDefaultAsync(v => v.Id == id, ct);

            if (evaluationMembershipToDelete == null)
                throw new EntityNotFoundException<SAVM.EvaluationMembership>();

            _context.EvaluationMemberships.Remove(evaluationMembershipToDelete);
            await _context.SaveChangesAsync(ct);

            return;
        }

    }
}
