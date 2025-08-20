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
using Microsoft.Extensions.Logging;
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
        Task<IEnumerable<ViewModels.TeamUser>> GetByTeamAsync(Guid teamId, CancellationToken ct);
        Task<ViewModels.TeamUser> GetAsync(Guid id, CancellationToken ct);
        Task<ViewModels.TeamUser> CreateAsync(ViewModels.TeamUser teamUser, CancellationToken ct);
        Task<ViewModels.TeamUser> SetObserverAsync(Guid id, bool value, CancellationToken ct);
        Task<ViewModels.TeamUser> SetIncrementerAsync(Guid id, bool value, CancellationToken ct);
        Task<ViewModels.TeamUser> SetManagerAsync(Guid id, bool value, CancellationToken ct);
        Task<ViewModels.TeamUser> SetModifierAsync(Guid id, bool value, CancellationToken ct);
        Task<ViewModels.TeamUser> SetSubmitterAsync(Guid id, bool value, CancellationToken ct);
        Task<bool> DeleteAsync(Guid id, CancellationToken ct);
        Task<bool> DeleteByIdsAsync(Guid teamId, Guid userId, CancellationToken ct);
    }

    public class TeamUserService : ITeamUserService
    {
        private readonly CiteContext _context;
        private readonly IAuthorizationService _authorizationService;
        private readonly ClaimsPrincipal _user;
        private readonly IMapper _mapper;
        private readonly ILogger<ITeamUserService> _logger;

        public TeamUserService(
            CiteContext context,
            IAuthorizationService authorizationService,
            IPrincipal user,
            ILogger<ITeamUserService> logger,
            IMapper mapper)
        {
            _context = context;
            _authorizationService = authorizationService;
            _user = user as ClaimsPrincipal;
            _mapper = mapper;
            _logger = logger;
        }

        public async Task<IEnumerable<ViewModels.TeamUser>> GetByEvaluationAsync(Guid evaluationId, CancellationToken ct)
        {
            if (!(await _authorizationService.AuthorizeAsync(_user, null, new EvaluationUserRequirement(evaluationId, _context))).Succeeded)
                throw new ForbiddenException();

            var items = await _context.TeamUsers
                .Where(tu => tu.Team.EvaluationId == evaluationId)
                .ToListAsync(ct);

            return _mapper.Map<IEnumerable<TeamUser>>(items);
        }

        public async Task<IEnumerable<ViewModels.TeamUser>> GetByTeamAsync(Guid teamId, CancellationToken ct)
        {
            var team = await _context.Teams.SingleOrDefaultAsync(t => t.Id == teamId);
            if (team == null)
                throw new EntityNotFoundException<Team>();

            if (!(await _authorizationService.AuthorizeAsync(_user, null, new EvaluationUserRequirement((Guid)team.EvaluationId, _context))).Succeeded)
                throw new ForbiddenException();

            var items = await _context.TeamUsers
                .Where(tu => tu.TeamId == teamId)
                .Include(tu => tu.User)
                .ToListAsync(ct);

            return _mapper.Map<IEnumerable<TeamUser>>(items);
        }

        public async Task<ViewModels.TeamUser> GetAsync(Guid id, CancellationToken ct)
        {
            if (!(await _authorizationService.AuthorizeAsync(_user, null, new ContentDeveloperRequirement())).Succeeded)
                throw new ForbiddenException();

            var item = await _context.TeamUsers
                .Include(tu => tu.User)
                .SingleOrDefaultAsync(o => o.Id == id, ct);

            return _mapper.Map<TeamUser>(item);
        }

        public async Task<ViewModels.TeamUser> CreateAsync(ViewModels.TeamUser teamUser, CancellationToken ct)
        {
            if (!(await _authorizationService.AuthorizeAsync(_user, null, new ContentDeveloperRequirement())).Succeeded)
                throw new ForbiddenException();

            // make sure this would not add a duplicate user on any pending or active evaluations
            var requestedUser = await _context.Users.FindAsync(teamUser.UserId);
            var requestedTeam = await _context.Teams.FindAsync(teamUser.TeamId);
            var existingevaluations = await _context.Teams
                .Where(t => (t.Evaluation.Status == Data.Enumerations.ItemStatus.Active || t.Evaluation.Status == Data.Enumerations.ItemStatus.Pending) &&
                    t.Id == teamUser.TeamId)
                .Select(et => et.Evaluation)
                .ToListAsync(ct);
                var message = "";
            foreach (var evaluation in existingevaluations)
            {
                var teams = await _context.Teams
                    .Where(t => t.EvaluationId == evaluation.Id)
                    .ToListAsync(ct);
                foreach (var team in teams)
                {
                    if (await _context.TeamUsers.AnyAsync(tu => tu.TeamId == team.Id && tu.UserId == teamUser.UserId, ct))
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
            _logger.LogWarning($"User {teamUser.UserId} added to team {teamUser.TeamId} by {_user.GetId()}");
            return await GetAsync(teamUserEntity.Id, ct);
        }

        public async Task<ViewModels.TeamUser> SetObserverAsync(Guid id, bool value, CancellationToken ct)
        {
            if (!(await _authorizationService.AuthorizeAsync(_user, null, new ContentDeveloperRequirement())).Succeeded)
                throw new ForbiddenException();

            var teamUserToUpdate = await _context.TeamUsers
                .Include(tu => tu.User)
                .SingleOrDefaultAsync(v => v.Id == id, ct);
            if (teamUserToUpdate == null)
                throw new EntityNotFoundException<TeamUser>();

            teamUserToUpdate.IsObserver = value;
            await _context.SaveChangesAsync(ct);
            if (value)
            {
                _logger.LogWarning($"User {teamUserToUpdate.UserId} set as observer on team {teamUserToUpdate.TeamId} by {_user.GetId()}");
            }
            else
            {
                _logger.LogWarning($"User {teamUserToUpdate.UserId} removed as observer on team {teamUserToUpdate.TeamId} by {_user.GetId()}");
            }
            return _mapper.Map<TeamUser>(teamUserToUpdate);
        }

        public async Task<ViewModels.TeamUser> SetIncrementerAsync(Guid id, bool value, CancellationToken ct)
        {
            if (!(await _authorizationService.AuthorizeAsync(_user, null, new ContentDeveloperRequirement())).Succeeded)
                throw new ForbiddenException();

            var teamUserToUpdate = await _context.TeamUsers
                .Include(tu => tu.User)
                .SingleOrDefaultAsync(v => v.Id == id, ct);
            if (teamUserToUpdate == null)
                throw new EntityNotFoundException<TeamUser>();

            teamUserToUpdate.CanIncrementMove = value;
            await _context.SaveChangesAsync(ct);
            if (value)
            {
                _logger.LogWarning($"User {teamUserToUpdate.UserId} set as incrementer on team {teamUserToUpdate.TeamId} by {_user.GetId()}");
            }
            else
            {
                _logger.LogWarning($"User {teamUserToUpdate.UserId} removed as incrementer on team {teamUserToUpdate.TeamId} by {_user.GetId()}");
            }
            return _mapper.Map<TeamUser>(teamUserToUpdate);
        }

        public async Task<ViewModels.TeamUser> SetManagerAsync(Guid id, bool value, CancellationToken ct)
        {
            if (!(await _authorizationService.AuthorizeAsync(_user, null, new ContentDeveloperRequirement())).Succeeded)
                throw new ForbiddenException();

            var teamUserToUpdate = await _context.TeamUsers
                .Include(tu => tu.User)
                .SingleOrDefaultAsync(v => v.Id == id, ct);
            if (teamUserToUpdate == null)
                throw new EntityNotFoundException<TeamUser>();

            teamUserToUpdate.CanManageTeam = value;
            await _context.SaveChangesAsync(ct);
            if (value)
            {
                _logger.LogWarning($"User {teamUserToUpdate.UserId} set as manager on team {teamUserToUpdate.TeamId} by {_user.GetId()}");
            }
            else
            {
                _logger.LogWarning($"User {teamUserToUpdate.UserId} removed as manager on team {teamUserToUpdate.TeamId} by {_user.GetId()}");
            }
            return _mapper.Map<TeamUser>(teamUserToUpdate);
        }

        public async Task<ViewModels.TeamUser> SetModifierAsync(Guid id, bool value, CancellationToken ct)
        {
            if (!(await _authorizationService.AuthorizeAsync(_user, null, new ContentDeveloperRequirement())).Succeeded)
                throw new ForbiddenException();

            var teamUserToUpdate = await _context.TeamUsers
                .Include(tu => tu.User)
                .SingleOrDefaultAsync(v => v.Id == id, ct);
            if (teamUserToUpdate == null)
                throw new EntityNotFoundException<TeamUser>();

            teamUserToUpdate.CanModify = value;
            await _context.SaveChangesAsync(ct);
            if (value)
            {
                _logger.LogWarning($"User {teamUserToUpdate.UserId} set as modifier on team {teamUserToUpdate.TeamId} by {_user.GetId()}");
            }
            else
            {
                _logger.LogWarning($"User {teamUserToUpdate.UserId} removed as modifier on team {teamUserToUpdate.TeamId} by {_user.GetId()}");
            }
            return _mapper.Map<TeamUser>(teamUserToUpdate);
        }

        public async Task<ViewModels.TeamUser> SetSubmitterAsync(Guid id, bool value, CancellationToken ct)
        {
            if (!(await _authorizationService.AuthorizeAsync(_user, null, new ContentDeveloperRequirement())).Succeeded)
                throw new ForbiddenException();

            var teamUserToUpdate = await _context.TeamUsers
                .Include(tu => tu.User)
                .SingleOrDefaultAsync(v => v.Id == id, ct);
            if (teamUserToUpdate == null)
                throw new EntityNotFoundException<TeamUser>();

            teamUserToUpdate.CanSubmit = value;
            await _context.SaveChangesAsync(ct);
            if (value)
            {
                _logger.LogWarning($"User {teamUserToUpdate.UserId} set as submitter on team {teamUserToUpdate.TeamId} by {_user.GetId()}");
            }
            else
            {
                _logger.LogWarning($"User {teamUserToUpdate.UserId} removed as submitter on team {teamUserToUpdate.TeamId} by {_user.GetId()}");
            }
            return _mapper.Map<TeamUser>(teamUserToUpdate);
        }

        public async Task<bool> DeleteAsync(Guid id, CancellationToken ct)
        {
            if (!(await _authorizationService.AuthorizeAsync(_user, null, new ContentDeveloperRequirement())).Succeeded)
                throw new ForbiddenException();

            var teamUserToDelete = await _context.TeamUsers.Include(m => m.Team).SingleOrDefaultAsync(v => v.Id == id, ct);

            if (teamUserToDelete == null)
                throw new EntityNotFoundException<TeamUser>();

            await DeleteAsync(teamUserToDelete, ct);
            return true;
        }

        public async Task<bool> DeleteByIdsAsync(Guid teamId, Guid userId, CancellationToken ct)
        {
            if (!(await _authorizationService.AuthorizeAsync(_user, null, new ContentDeveloperRequirement())).Succeeded)
                throw new ForbiddenException();

            var teamUserToDelete = await _context.TeamUsers.Include(m => m.Team).SingleOrDefaultAsync(v => (v.UserId == userId) && (v.TeamId == teamId), ct);

            if (teamUserToDelete == null)
                throw new EntityNotFoundException<TeamUser>();

            await DeleteAsync(teamUserToDelete, ct);
            return true;
        }

        private async Task<bool> DeleteAsync(TeamUserEntity teamUserToDelete, CancellationToken ct)
        {
            // remove the roles this user has been assigned to
            var teamRoleIds = await _context.Roles.Where(r => r.TeamId == teamUserToDelete.TeamId).Select(r => r.Id).ToListAsync(ct);
            var roleUsers = await _context.RoleUsers.Where(ru => teamRoleIds.Contains(ru.RoleId) && ru.UserId == teamUserToDelete.UserId).ToListAsync(ct);
            _context.RoleUsers.RemoveRange(roleUsers);
            //remove the user from the team
            _context.TeamUsers.Remove(teamUserToDelete);
            await _context.SaveChangesAsync(ct);
            _logger.LogWarning($"User {teamUserToDelete.UserId} removed from team {teamUserToDelete.TeamId} by {_user.GetId()}");
            await DeleteUserSubmissions((Guid)teamUserToDelete.Team.EvaluationId, teamUserToDelete.UserId, ct);
            return true;
        }

        public async Task DeleteUserSubmissions(Guid evaluationId, Guid userId, CancellationToken ct)
        {
            var submissions = await _context.Submissions.Where(m => m.EvaluationId == evaluationId && m.UserId == userId).ToListAsync(ct);
            _context.RemoveRange(submissions);
            await _context.SaveChangesAsync(ct);
        }

    }
}
