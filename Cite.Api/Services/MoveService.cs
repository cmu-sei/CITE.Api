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
        Task<ViewModels.Move> UpdateAsync(Guid id, ViewModels.UpdateMoveRequest move, CancellationToken ct);
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
            var moves = _context.Moves.Where(move => move.EvaluationId == evaluationId);
            if (!hasPermission)
            {
                // a member of one of the evaluation's teams sees the moves up to the current one
                if (!await IsOnATeamAsync(evaluationId, ct))
                    throw new ForbiddenException();
                moves = moves.Where(move => move.MoveNumber <= move.Evaluation.CurrentMoveNumber);
            }
            var moveEntities = await moves.ToListAsync(ct);

            return _mapper.Map<IEnumerable<Move>>(moveEntities).ToList();
        }

        public async Task<ViewModels.Move> GetAsync(Guid id, bool hasPermission, CancellationToken ct)
        {
            var item = await _context.Moves
                .Where(m => m.Id == id)
                .Select(m => new { Move = m, EvaluationId = m.EvaluationId, CurrentMoveNumber = m.Evaluation.CurrentMoveNumber })
                .SingleOrDefaultAsync(ct);
            if (item == null)
                throw new EntityNotFoundException<Move>();
            if (!hasPermission)
            {
                if (!await IsOnATeamAsync(item.EvaluationId, ct) || item.Move.MoveNumber > item.CurrentMoveNumber)
                    throw new ForbiddenException();
            }
            return _mapper.Map<Move>(item.Move);
        }

        private Task<bool> IsOnATeamAsync(Guid evaluationId, CancellationToken ct)
        {
            var userId = _user.GetId();
            return _context.TeamMemberships.AnyAsync(m => m.UserId == userId && m.Team.EvaluationId == evaluationId, ct);
        }

        public async Task<ViewModels.Move> CreateAsync(ViewModels.Move move, CancellationToken ct)
        {
            move.Id = move.Id != Guid.Empty ? move.Id : Guid.NewGuid();
            move.CreatedBy = _user.GetId();
            var moveEntity = _mapper.Map<MoveEntity>(move);
            moveEntity.SituationTime = moveEntity.SituationTime.ToUniversalTime();
            _context.Moves.Add(moveEntity);
            await SaveMoveAsync(ct);
            move = await GetAsync(moveEntity.Id, true, ct);

            return move;
        }

        public async Task<ViewModels.Move> UpdateAsync(Guid id, ViewModels.UpdateMoveRequest move, CancellationToken ct)
        {
            var moveToUpdate = await _context.Moves.SingleOrDefaultAsync(v => v.Id == id, ct);
            if (moveToUpdate == null)
                throw new EntityNotFoundException<Move>();

            moveToUpdate.Description = move.Description;
            moveToUpdate.MoveNumber = move.MoveNumber;
            moveToUpdate.SituationTime = move.SituationTime.ToUniversalTime();
            moveToUpdate.SituationDescription = move.SituationDescription;
            moveToUpdate.ModifiedBy = _user.GetId();
            await SaveMoveAsync(ct);

            return await GetAsync(moveToUpdate.Id, true, ct);
        }

        /// <summary>A move number is unique in its evaluation; a second move with the same number is a 409.</summary>
        private async Task SaveMoveAsync(CancellationToken ct)
        {
            try
            {
                await _context.SaveChangesAsync(ct);
            }
            catch (DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException { SqlState: Npgsql.PostgresErrorCodes.UniqueViolation, ConstraintName: "IX_moves_evaluation_id_move_number" })
            {
                throw new ConflictException("The evaluation already has a move with this move number.");
            }
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
