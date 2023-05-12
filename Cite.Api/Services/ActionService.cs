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
using Cite.Api.Infrastructure.Options;
using Cite.Api.ViewModels;

namespace Cite.Api.Services
{
    public interface IActionService
    {
        Task<IEnumerable<ViewModels.Action>> GetByEvaluationTeamAsync(Guid evaluationId, Guid teamId, CancellationToken ct);
        Task<IEnumerable<ViewModels.Action>> GetByEvaluationMoveAsync(Guid evaluationId, int moveNumber, CancellationToken ct);
        Task<IEnumerable<ViewModels.Action>> GetByEvaluationMoveTeamAsync(Guid evaluationId, int moveNumber, Guid teamId, CancellationToken ct);
        Task<ViewModels.Action> GetAsync(Guid id, CancellationToken ct);
        Task<ViewModels.Action> CreateAsync(ViewModels.Action action, CancellationToken ct);
        Task<ViewModels.Action> UpdateAsync(Guid id, ViewModels.Action action, CancellationToken ct);
        Task<ViewModels.Action> SetIsCheckedAsync(Guid id, bool value, CancellationToken ct);
        Task<bool> DeleteAsync(Guid id, CancellationToken ct);
    }

    public class ActionService : IActionService
    {
        private readonly CiteContext _context;
        private readonly IAuthorizationService _authorizationService;
        private readonly ClaimsPrincipal _user;
        private readonly IMapper _mapper;
         private readonly DatabaseOptions _options;

        public ActionService(
            CiteContext context,
            IAuthorizationService authorizationService,
            IPrincipal user,
            IMapper mapper,
            DatabaseOptions options)
        {
            _context = context;
            _authorizationService = authorizationService;
            _user = user as ClaimsPrincipal;
            _mapper = mapper;
            _options = options;
        }

        public async Task<IEnumerable<ViewModels.Action>> GetByEvaluationTeamAsync(Guid evaluationId, Guid teamId, CancellationToken ct)
        {
            if (!(await _authorizationService.AuthorizeAsync(_user, null, new TeamUserRequirement(teamId))).Succeeded &&
                !(await _authorizationService.AuthorizeAsync(_user, null, new EvaluationObserverRequirement(evaluationId))).Succeeded
            )
                throw new ForbiddenException();

            var actionEntities = await _context.Actions
                .Where(a => a.EvaluationId == evaluationId &&
                            a.TeamId == teamId)
                .OrderBy(a => a.ActionNumber)
                .ToListAsync(ct);
            var actions = _mapper.Map<IEnumerable<ViewModels.Action>>(actionEntities).ToList();

            return actions;
        }

        public async Task<IEnumerable<ViewModels.Action>> GetByEvaluationMoveAsync(Guid evaluationId, int moveNumber, CancellationToken ct)
        {
            if (!(await _authorizationService.AuthorizeAsync(_user, null, new ContentDeveloperRequirement())).Succeeded)
                throw new ForbiddenException();

            var actionEntities = await _context.Actions
                .Where(a => a.EvaluationId == evaluationId &&
                            a.MoveNumber == moveNumber)
                .OrderBy(a => a.Team.ShortName)
                .ThenBy(a => a.Team.Name)
                .ThenBy(a => a.ActionNumber)
                .ToListAsync(ct);
            var actions = _mapper.Map<IEnumerable<ViewModels.Action>>(actionEntities).ToList();

            return actions;
        }

        public async Task<IEnumerable<ViewModels.Action>> GetByEvaluationMoveTeamAsync(Guid evaluationId, int moveNumber, Guid teamId, CancellationToken ct)
        {
            if (!(await _authorizationService.AuthorizeAsync(_user, null, new TeamUserRequirement(teamId))).Succeeded)
                throw new ForbiddenException();

            var actionEntities = await _context.Actions
                .Where(a => a.EvaluationId == evaluationId &&
                            a.MoveNumber == moveNumber &&
                            a.TeamId == teamId)
                .OrderBy(a => a.ActionNumber)
                .ToListAsync(ct);
            var actions = _mapper.Map<IEnumerable<ViewModels.Action>>(actionEntities).ToList();

            return actions;
        }

        public async Task<ViewModels.Action> GetAsync(Guid id, CancellationToken ct)
        {
            var item = await _context.Actions
                .SingleAsync(a => a.Id == id, ct);

            if (item == null)
                throw new EntityNotFoundException<ActionEntity>();

            if (!(await _authorizationService.AuthorizeAsync(_user, null, new TeamUserRequirement(item.TeamId))).Succeeded)
                throw new ForbiddenException();

            return _mapper.Map<ViewModels.Action>(item);
        }

        public async Task<ViewModels.Action> CreateAsync(ViewModels.Action action, CancellationToken ct)
        {
            // user must be on the requested team and be able to submit
            if (
                !(
                    (await _authorizationService.AuthorizeAsync(_user, null, new TeamUserRequirement(action.TeamId))).Succeeded &&
                    (await _authorizationService.AuthorizeAsync(_user, null, new CanSubmitRequirement())).Succeeded
                ) &&
                !(await _authorizationService.AuthorizeAsync(_user, null, new ContentDeveloperRequirement())).Succeeded
            )
                throw new ForbiddenException();

            action.Id = action.Id != Guid.Empty ? action.Id : Guid.NewGuid();
            action.DateCreated = DateTime.UtcNow;
            action.CreatedBy = _user.GetId();
            action.DateModified = null;
            action.ModifiedBy = null;
            var actionEntity = _mapper.Map<ActionEntity>(action);

            _context.Actions.Add(actionEntity);
            await _context.SaveChangesAsync(ct);


            return _mapper.Map<ViewModels.Action>(actionEntity);
        }

        public async Task<ViewModels.Action> UpdateAsync(Guid id, ViewModels.Action action, CancellationToken ct)
        {
            // user must be on the requested team and be able to submit
            if (
                !(
                    (await _authorizationService.AuthorizeAsync(_user, null, new TeamUserRequirement(action.TeamId))).Succeeded &&
                    (await _authorizationService.AuthorizeAsync(_user, null, new CanSubmitRequirement())).Succeeded
                ) &&
                !(await _authorizationService.AuthorizeAsync(_user, null, new ContentDeveloperRequirement())).Succeeded
            )
                throw new ForbiddenException();

            var actionToUpdate = await _context.Actions.SingleOrDefaultAsync(v => v.Id == id, ct);
            if (actionToUpdate == null)
                throw new EntityNotFoundException<ActionEntity>();

            action.CreatedBy = actionToUpdate.CreatedBy;
            action.DateCreated = actionToUpdate.DateCreated;
            _mapper.Map(action, actionToUpdate);
            actionToUpdate.ModifiedBy = _user.GetId();
            actionToUpdate.DateModified = DateTime.UtcNow;
            _context.Actions.Update(actionToUpdate);
            await _context.SaveChangesAsync(ct);

            return _mapper.Map<ViewModels.Action>(actionToUpdate);
        }

        public async Task<ViewModels.Action> SetIsCheckedAsync(Guid id, bool value, CancellationToken ct)
        {
            var actionToUpdate = await _context.Actions.SingleOrDefaultAsync(v => v.Id == id, ct);

            if (actionToUpdate == null)
                throw new EntityNotFoundException<ActionEntity>();

            // user must be on the requested team
            if (!(await _authorizationService.AuthorizeAsync(_user, null, new TeamUserRequirement(actionToUpdate.TeamId))).Succeeded)
                throw new ForbiddenException();

            actionToUpdate.IsChecked = value;
            actionToUpdate.ChangedBy = _user.GetId();
            await _context.SaveChangesAsync(ct);

            return _mapper.Map<ViewModels.Action>(actionToUpdate);
        }

        public async Task<bool> DeleteAsync(Guid id, CancellationToken ct)
        {
            var actionToDelete = await _context.Actions.SingleOrDefaultAsync(v => v.Id == id, ct);

            if (actionToDelete == null)
                throw new EntityNotFoundException<ActionEntity>();

            // user must be on the requested team and be able to submit
            if (
                !(
                    (await _authorizationService.AuthorizeAsync(_user, null, new TeamUserRequirement(actionToDelete.TeamId))).Succeeded &&
                    (await _authorizationService.AuthorizeAsync(_user, null, new CanSubmitRequirement())).Succeeded
                ) &&
                !(await _authorizationService.AuthorizeAsync(_user, null, new ContentDeveloperRequirement())).Succeeded
            )
                throw new ForbiddenException();

            _context.Actions.Remove(actionToDelete);
            await _context.SaveChangesAsync(ct);

            return true;
        }

    }

 }

