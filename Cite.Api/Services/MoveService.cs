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
        Task<IEnumerable<ViewModels.Move>> GetByEvaluationAsync(Guid evaluationId, CancellationToken ct);
        Task<ViewModels.Move> GetAsync(Guid id, CancellationToken ct);
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

        public async Task<IEnumerable<ViewModels.Move>> GetByEvaluationAsync(Guid evaluationId, CancellationToken ct)
        {
            if (!(await _authorizationService.AuthorizeAsync(_user, null, new EvaluationUserRequirement(evaluationId))).Succeeded)
                throw new ForbiddenException();

            var moveEntities = await _context.Moves
                .Where(move => move.EvaluationId == evaluationId)
                .ToListAsync();

            return _mapper.Map<IEnumerable<Move>>(moveEntities).ToList();;
        }

        public async Task<ViewModels.Move> GetAsync(Guid id, CancellationToken ct)
        {
            if (!(await _authorizationService.AuthorizeAsync(_user, null, new BaseUserRequirement())).Succeeded)
                throw new ForbiddenException();

            var item = await _context.Moves.SingleAsync(move => move.Id == id, ct);

            return _mapper.Map<Move>(item);
        }

        public async Task<ViewModels.Move> CreateAsync(ViewModels.Move move, CancellationToken ct)
        {
            if (!(await _authorizationService.AuthorizeAsync(_user, null, new ContentDeveloperRequirement())).Succeeded)
                throw new ForbiddenException();
            move.Id = move.Id != Guid.Empty ? move.Id : Guid.NewGuid();
            move.DateCreated = DateTime.UtcNow;
            move.CreatedBy = _user.GetId();
            move.DateModified = null;
            move.ModifiedBy = null;
            var moveEntity = _mapper.Map<MoveEntity>(move);
            moveEntity.SituationTime = moveEntity.SituationTime.ToUniversalTime();

            _context.Moves.Add(moveEntity);
            await _context.SaveChangesAsync(ct);
            move = await GetAsync(moveEntity.Id, ct);

            return move;
        }

        public async Task<ViewModels.Move> UpdateAsync(Guid id, ViewModels.Move move, CancellationToken ct)
        {
            if (!(await _authorizationService.AuthorizeAsync(_user, null, new ContentDeveloperRequirement())).Succeeded)
                throw new ForbiddenException();

            var moveToUpdate = await _context.Moves.SingleOrDefaultAsync(v => v.Id == id, ct);

            if (moveToUpdate == null)
                throw new EntityNotFoundException<Move>();

            move.CreatedBy = moveToUpdate.CreatedBy;
            move.DateCreated = moveToUpdate.DateCreated;
            move.ModifiedBy = _user.GetId();
            move.DateModified = DateTime.UtcNow;
            _mapper.Map(move, moveToUpdate);
            moveToUpdate.SituationTime = moveToUpdate.SituationTime.ToUniversalTime();

            _context.Moves.Update(moveToUpdate);
            await _context.SaveChangesAsync(ct);

            move = await GetAsync(moveToUpdate.Id, ct);

            return move;
        }

        public async Task<bool> DeleteAsync(Guid id, CancellationToken ct)
        {
            if (!(await _authorizationService.AuthorizeAsync(_user, null, new ContentDeveloperRequirement())).Succeeded)
                throw new ForbiddenException();

            var moveToDelete = await _context.Moves.SingleOrDefaultAsync(v => v.Id == id, ct);

            if (moveToDelete == null)
                throw new EntityNotFoundException<Move>();

            _context.Moves.Remove(moveToDelete);
            await _context.SaveChangesAsync(ct);

            return true;
        }

    }
}

