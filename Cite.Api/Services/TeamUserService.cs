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
    public interface ITeamUserService
    {
        Task<IEnumerable<ViewModels.TeamUser>> GetByEvaluationAsync(Guid evaluationId, CancellationToken ct);
        Task<ViewModels.TeamUser> GetAsync(Guid id, CancellationToken ct);
        Task<ViewModels.TeamUser> CreateAsync(ViewModels.TeamUser teamUser, CancellationToken ct);
        Task<ViewModels.TeamUser> SetObserverAsync(Guid id, bool value, CancellationToken ct);
        Task<bool> DeleteAsync(Guid id, CancellationToken ct);
        Task<bool> DeleteByIdsAsync(Guid teamId, Guid userId, CancellationToken ct);
    }

    public class TeamUserService : ITeamUserService
    {
        private readonly CiteContext _context;
        private readonly IAuthorizationService _authorizationService;
        private readonly ClaimsPrincipal _user;
        private readonly IMapper _mapper;

        public TeamUserService(CiteContext context, IAuthorizationService authorizationService, IPrincipal user, IMapper mapper)
        {
            _context = context;
            _authorizationService = authorizationService;
            _user = user as ClaimsPrincipal;
            _mapper = mapper;
        }

        public async Task<IEnumerable<ViewModels.TeamUser>> GetByEvaluationAsync(Guid evaluationId, CancellationToken ct)
        {
            if (!(await _authorizationService.AuthorizeAsync(_user, null, new EvaluationUserRequirement(evaluationId))).Succeeded)
                throw new ForbiddenException();

            var items = await _context.TeamUsers
                .Where(tu => tu.Team.EvaluationId == evaluationId)
                .ToListAsync(ct);

            return _mapper.Map<IEnumerable<TeamUser>>(items);
        }

        public async Task<ViewModels.TeamUser> GetAsync(Guid id, CancellationToken ct)
        {
            if (!(await _authorizationService.AuthorizeAsync(_user, null, new FullRightsRequirement())).Succeeded)
                throw new ForbiddenException();

            var item = await _context.TeamUsers
                .Include(tu => tu.User)
                .SingleOrDefaultAsync(o => o.Id == id, ct);

            return _mapper.Map<TeamUser>(item);
        }

        public async Task<ViewModels.TeamUser> CreateAsync(ViewModels.TeamUser teamUser, CancellationToken ct)
        {
            if (!(await _authorizationService.AuthorizeAsync(_user, null, new FullRightsRequirement())).Succeeded)
                throw new ForbiddenException();

            // make sure this would not add a duplicate user on any pending or active evaluations
            var requestedUser = await _context.Users.FindAsync(teamUser.UserId);
            var requestedTeam = await _context.Teams.FindAsync(teamUser.TeamId);
            var existingevaluations = await _context.Teams
                .Where(t => (t.Evaluation.Status == Data.Enumerations.ItemStatus.Active || t.Evaluation.Status == Data.Enumerations.ItemStatus.Pending) &&
                    t.Id == teamUser.TeamId)
                .Select(et => et.Evaluation)
                .ToListAsync();
                var message = "";
            foreach (var evaluation in existingevaluations)
            {
                var teams = await _context.Teams
                    .Where(t => t.EvaluationId == evaluation.Id)
                    .ToListAsync();
                foreach (var team in teams)
                {
                    if (await _context.TeamUsers.AnyAsync(tu => tu.TeamId == team.Id && tu.UserId == teamUser.UserId))
                    {
                        message = message + "Team: " + team.Name + " on Evaluation: " + evaluation.Description + "\n";
                    }
                }
            }
            if (message != "")
            {
                message = "Cannot add user to " + requestedTeam.Name + ", because " + requestedUser.Name + " is already on :\n" + message;
                throw new ArgumentException(message);
            }

            // okay to add this TeamUser
            teamUser.Id = teamUser.Id != Guid.Empty ? teamUser.Id : Guid.NewGuid();
            teamUser.DateCreated = DateTime.UtcNow;
            teamUser.CreatedBy = _user.GetId();
            teamUser.DateModified = null;
            teamUser.ModifiedBy = null;
            var teamUserEntity = _mapper.Map<TeamUserEntity>(teamUser);

            _context.TeamUsers.Add(teamUserEntity);
            await _context.SaveChangesAsync(ct);

            return await GetAsync(teamUserEntity.Id, ct);
        }

        public async Task<ViewModels.TeamUser> SetObserverAsync(Guid id, bool value, CancellationToken ct)
        {
            if (!(await _authorizationService.AuthorizeAsync(_user, null, new FullRightsRequirement())).Succeeded)
                throw new ForbiddenException();

            var teamUserToUpdate = await _context.TeamUsers.SingleOrDefaultAsync(v => v.Id == id, ct);
            if (teamUserToUpdate == null)
                throw new EntityNotFoundException<TeamUser>();

            teamUserToUpdate.IsObserver = value;
            await _context.SaveChangesAsync(ct);

            return _mapper.Map<TeamUser>(teamUserToUpdate);
        }

        public async Task<bool> DeleteAsync(Guid id, CancellationToken ct)
        {
            if (!(await _authorizationService.AuthorizeAsync(_user, null, new FullRightsRequirement())).Succeeded)
                throw new ForbiddenException();

            var teamUserToDelete = await _context.TeamUsers.SingleOrDefaultAsync(v => v.Id == id, ct);

            if (teamUserToDelete == null)
                throw new EntityNotFoundException<TeamUser>();

            _context.TeamUsers.Remove(teamUserToDelete);
            await _context.SaveChangesAsync(ct);

            return true;
        }

        public async Task<bool> DeleteByIdsAsync(Guid teamId, Guid userId, CancellationToken ct)
        {
            if (!(await _authorizationService.AuthorizeAsync(_user, null, new FullRightsRequirement())).Succeeded)
                throw new ForbiddenException();

            var teamUserToDelete = await _context.TeamUsers.SingleOrDefaultAsync(v => (v.UserId == userId) && (v.TeamId == teamId), ct);

            if (teamUserToDelete == null)
                throw new EntityNotFoundException<TeamUser>();

            _context.TeamUsers.Remove(teamUserToDelete);
            await _context.SaveChangesAsync(ct);

            return true;
        }

    }
}

