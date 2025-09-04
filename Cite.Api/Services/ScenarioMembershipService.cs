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
    public interface IEvaluationMembershipService
    {
        STT.Task<ViewModels.EvaluationMembership> GetAsync(Guid id, CancellationToken ct);
        STT.Task<IEnumerable<ViewModels.EvaluationMembership>> GetByEvaluationAsync(Guid evaluationId, CancellationToken ct);
        STT.Task<ViewModels.EvaluationMembership> CreateAsync(ViewModels.EvaluationMembership evaluationMembership, CancellationToken ct);
        STT.Task<ViewModels.EvaluationMembership> UpdateAsync(Guid id, ViewModels.EvaluationMembership evaluationMembership, CancellationToken ct);
        STT.Task DeleteAsync(Guid id, CancellationToken ct);
    }

    public class EvaluationMembershipService : IEvaluationMembershipService
    {
        private readonly CiteContext _context;
        private readonly ClaimsPrincipal _user;
        private readonly IMapper _mapper;

        public EvaluationMembershipService(CiteContext context, IPrincipal user, IMapper mapper)
        {
            _context = context;
            _user = user as ClaimsPrincipal;
            _mapper = mapper;
        }

        public async STT.Task<ViewModels.EvaluationMembership> GetAsync(Guid id, CancellationToken ct)
        {
            var item = await _context.EvaluationMemberships
                .SingleOrDefaultAsync(o => o.Id == id, ct);

            if (item == null)
                throw new EntityNotFoundException<EvaluationMembership>();

            return _mapper.Map<SAVM.EvaluationMembership>(item);
        }

        public async STT.Task<IEnumerable<ViewModels.EvaluationMembership>> GetByEvaluationAsync(Guid evaluationId, CancellationToken ct)
        {
            var items = await _context.EvaluationMemberships
                .Where(m => m.EvaluationId == evaluationId)
                .ToListAsync(ct);

            return _mapper.Map<IEnumerable<SAVM.EvaluationMembership>>(items);
        }

        public async STT.Task<ViewModels.EvaluationMembership> CreateAsync(ViewModels.EvaluationMembership evaluationMembership, CancellationToken ct)
        {
            var evaluationMembershipEntity = _mapper.Map<EvaluationMembershipEntity>(evaluationMembership);

            _context.EvaluationMemberships.Add(evaluationMembershipEntity);
            await _context.SaveChangesAsync(ct);
            var evaluation = await GetAsync(evaluationMembershipEntity.Id, ct);

            return evaluation;
        }
        public async STT.Task<ViewModels.EvaluationMembership> UpdateAsync(Guid id, ViewModels.EvaluationMembership evaluationMembership, CancellationToken ct)
        {
            var evaluationMembershipToUpdate = await _context.EvaluationMemberships.SingleOrDefaultAsync(v => v.Id == id, ct);
            if (evaluationMembershipToUpdate == null)
                throw new EntityNotFoundException<SAVM.Evaluation>();

            evaluationMembershipToUpdate.Role = null;
            evaluationMembershipToUpdate.RoleId = evaluationMembership.RoleId;
            await _context.SaveChangesAsync(ct);

            return _mapper.Map<SAVM.EvaluationMembership>(evaluationMembershipToUpdate);
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
