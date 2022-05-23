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
using Cite.Api.ViewModels;

namespace Cite.Api.Services
{
    public interface IEvaluationTeamService
    {
        Task<IEnumerable<ViewModels.EvaluationTeam>> GetAsync(CancellationToken ct);
        Task<ViewModels.EvaluationTeam> GetAsync(Guid id, CancellationToken ct);
        Task<ViewModels.EvaluationTeam> CreateAsync(ViewModels.EvaluationTeam evaluationTeam, CancellationToken ct);
        Task<bool> DeleteAsync(Guid id, CancellationToken ct);
        Task<bool> DeleteByIdsAsync(Guid evaluationId, Guid teamId, CancellationToken ct);
    }

    public class EvaluationTeamService : IEvaluationTeamService
    {
        private readonly CiteContext _context;
        private readonly IAuthorizationService _authorizationService;
        private readonly ClaimsPrincipal _user;
        private readonly IMapper _mapper;

        public EvaluationTeamService(CiteContext context, IAuthorizationService authorizationService, IPrincipal team, IMapper mapper)
        {
            _context = context;
            _authorizationService = authorizationService;
            _user = team as ClaimsPrincipal;
            _mapper = mapper;
        }

        public async Task<IEnumerable<ViewModels.EvaluationTeam>> GetAsync(CancellationToken ct)
        {
            if (!(await _authorizationService.AuthorizeAsync(_user, null, new ContentDeveloperRequirement())).Succeeded)
                throw new ForbiddenException();

            var items = await _context.EvaluationTeams
                .ToListAsync(ct);

            return _mapper.Map<IEnumerable<EvaluationTeam>>(items);
        }

        public async Task<ViewModels.EvaluationTeam> GetAsync(Guid id, CancellationToken ct)
        {
            if (!(await _authorizationService.AuthorizeAsync(_user, null, new ContentDeveloperRequirement())).Succeeded)
                throw new ForbiddenException();

            var item = await _context.EvaluationTeams
                .SingleOrDefaultAsync(o => o.Id == id, ct);

            return _mapper.Map<EvaluationTeam>(item);
        }

        public async Task<ViewModels.EvaluationTeam> CreateAsync(ViewModels.EvaluationTeam evaluationTeam, CancellationToken ct)
        {
            if (!(await _authorizationService.AuthorizeAsync(_user, null, new ContentDeveloperRequirement())).Succeeded)
                throw new ForbiddenException();

            // make sure adding the team will not duplicate any existing evaluation users
            var existingTeamIds = await _context.EvaluationTeams
                .Where(et => et.Evaluation.Status == Data.Enumerations.ItemStatus.Active || et.Evaluation.Status == Data.Enumerations.ItemStatus.Pending)
                .Select(et => et.Team.Id)
                .ToListAsync();
            var newTeamUserIds = await _context.TeamUsers
                .Where(tu => tu.TeamId == evaluationTeam.TeamId)
                .Select(tu => tu.UserId)
                .ToListAsync();
            var duplicateUsers = await _context.TeamUsers
                .Where(tu => tu.TeamId != evaluationTeam.TeamId && existingTeamIds.Contains(tu.TeamId) && newTeamUserIds.Contains(tu.UserId))
                .Select(tu => new { TeamName = tu.Team.Name, UserName = tu.User.Name})
                .ToListAsync();
            if (duplicateUsers.Any())
            {
                var message = "Adding this team would duplicate the following user(s) in this evaluation:\n";
                foreach (var duplicate in duplicateUsers)
                {
                    message = message + "Team: " + duplicate.TeamName + ", User: " + duplicate.UserName + "\n";
                }
                throw new ArgumentException(message);
            }
            // okay to add team to evaluation
            evaluationTeam.DateCreated = DateTime.UtcNow;
            evaluationTeam.CreatedBy = _user.GetId();
            evaluationTeam.DateModified = null;
            evaluationTeam.ModifiedBy = null;
            var evaluationTeamEntity = _mapper.Map<EvaluationTeamEntity>(evaluationTeam);
            evaluationTeamEntity.Id = evaluationTeamEntity.Id != Guid.Empty ? evaluationTeamEntity.Id : Guid.NewGuid();

            _context.EvaluationTeams.Add(evaluationTeamEntity);
            await _context.SaveChangesAsync(ct);

            return await GetAsync(evaluationTeamEntity.Id, ct);
        }

        public async Task<bool> DeleteAsync(Guid id, CancellationToken ct)
        {
            if (!(await _authorizationService.AuthorizeAsync(_user, null, new ContentDeveloperRequirement())).Succeeded)
                throw new ForbiddenException();

            var evaluationTeamToDelete = await _context.EvaluationTeams.SingleOrDefaultAsync(v => v.Id == id, ct);

            if (evaluationTeamToDelete == null)
                throw new EntityNotFoundException<EvaluationTeam>();

            _context.EvaluationTeams.Remove(evaluationTeamToDelete);
            await _context.SaveChangesAsync(ct);

            return true;
        }

        public async Task<bool> DeleteByIdsAsync(Guid evaluationId, Guid teamId, CancellationToken ct)
        {
            if (!(await _authorizationService.AuthorizeAsync(_user, null, new ContentDeveloperRequirement())).Succeeded)
                throw new ForbiddenException();

            var evaluationTeamToDelete = await _context.EvaluationTeams.SingleOrDefaultAsync(v => (v.TeamId == teamId) && (v.EvaluationId == evaluationId), ct);

            if (evaluationTeamToDelete == null)
                throw new EntityNotFoundException<EvaluationTeam>();

            _context.EvaluationTeams.Remove(evaluationTeamToDelete);
            await _context.SaveChangesAsync(ct);

            return true;
        }

    }
}

