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
    public interface IMoveService
    {
        Task<IEnumerable<ViewModels.Move>> GetByEvaluationAsync(Guid evaluationId, bool hasPermission, CancellationToken ct);
        Task<ViewModels.Move> GetAsync(Guid id, bool hasPermission, CancellationToken ct);
        Task<ViewModels.Move> CreateAsync(ViewModels.Move move, CancellationToken ct);
        Task<ViewModels.Move> UpdateAsync(Guid id, ViewModels.Move move, CancellationToken ct);
        Task<bool> DeleteAsync(Guid id, CancellationToken ct);
    }

    public class MoveService : IMoveService
    {
        private readonly CiteContext _context;
        private readonly IAuthorizationService _authorizationService;
        private readonly ClaimsPrincipal _user;
        private readonly IMapper _mapper;

        public MoveService(
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

        public async Task<IEnumerable<ViewModels.Move>> GetByEvaluationAsync(Guid evaluationId, bool hasPermission, CancellationToken ct)
        {
            if (!hasPermission)
            {
                if (!await _context.TeamMemberships.AnyAsync(m => m.Team.EvaluationId == evaluationId))
                    throw new ForbiddenException();
            }
            var moveEntities = await _context.Moves
                .Where(move => move.EvaluationId == evaluationId)
                .ToListAsync();

            return _mapper.Map<IEnumerable<Move>>(moveEntities).ToList();;
        }

        public async Task<ViewModels.Move> GetAsync(Guid id, bool hasPermission, CancellationToken ct)
        {
            var item = await _context.Moves
                .Where(m => m.Id == id)
                .Select(m => new { Move = m, EvaluationId = m.EvaluationId, CurrentMoveNumber = m.Evaluation.CurrentMoveNumber })
                .SingleAsync(ct);
            if (!hasPermission)
            {
                if (!await _context.TeamMemberships.AnyAsync(m => m.Team.EvaluationId == item.EvaluationId) || item.Move.MoveNumber > item.CurrentMoveNumber)
                    throw new ForbiddenException();
            }
            return _mapper.Map<Move>(item.Move);
        }

        public async Task<ViewModels.Move> CreateAsync(ViewModels.Move move, CancellationToken ct)
        {
            move.Id = move.Id != Guid.Empty ? move.Id : Guid.NewGuid();
            move.CreatedBy = _user.GetId();
            var moveEntity = _mapper.Map<MoveEntity>(move);
            moveEntity.SituationTime = moveEntity.SituationTime.ToUniversalTime();
            _context.Moves.Add(moveEntity);
            await _context.SaveChangesAsync(ct);
            move = await GetAsync(moveEntity.Id, true, ct);

            return move;
        }

        public async Task<ViewModels.Move> UpdateAsync(Guid id, ViewModels.Move move, CancellationToken ct)
        {
            var moveToUpdate = await _context.Moves.SingleOrDefaultAsync(v => v.Id == id, ct);
            if (moveToUpdate == null)
                throw new EntityNotFoundException<Move>();

            move.ModifiedBy = _user.GetId();
            _mapper.Map(move, moveToUpdate);
            moveToUpdate.SituationTime = moveToUpdate.SituationTime.ToUniversalTime();

            _context.Moves.Update(moveToUpdate);
            await _context.SaveChangesAsync(ct);

            move = await GetAsync(moveToUpdate.Id, true, ct);

            return move;
        }

        public async Task<bool> DeleteAsync(Guid id, CancellationToken ct)
        {
            var moveToDelete = await _context.Moves.SingleOrDefaultAsync(v => v.Id == id, ct);
            if (moveToDelete == null)
                throw new EntityNotFoundException<Move>();

            _context.Moves.Remove(moveToDelete);
            await _context.SaveChangesAsync(ct);
            await DeleteMoveSubmissions(moveToDelete.EvaluationId, moveToDelete.MoveNumber, ct);

            return true;
        }

        public async Task DeleteMoveSubmissions(Guid evaluationId, int moveNumber, CancellationToken ct)
        {
            var submissions = await _context.Submissions.Where(m => m.EvaluationId == evaluationId && m.MoveNumber == moveNumber).ToListAsync(ct);
            _context.RemoveRange(submissions);
            await _context.SaveChangesAsync(ct);
        }

    }
}
