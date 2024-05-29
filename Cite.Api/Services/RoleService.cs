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
    public interface IRoleService
    {
        Task<IEnumerable<ViewModels.Role>> GetByEvaluationAsync(Guid evaluationId, CancellationToken ct);
        Task<IEnumerable<ViewModels.Role>> GetByEvaluationTeamAsync(Guid evaluationId, Guid teamId, CancellationToken ct);
        Task<ViewModels.Role> GetAsync(Guid id, CancellationToken ct);
        Task<ViewModels.Role> CreateAsync(ViewModels.Role role, CancellationToken ct);
        Task<ViewModels.Role> UpdateAsync(Guid id, ViewModels.Role role, CancellationToken ct);
        Task<ViewModels.Role> AddUserAsync(Guid roleId, Guid userId, CancellationToken ct);
        Task<ViewModels.Role> RemoveUserAsync(Guid roleId, Guid userId, CancellationToken ct);
        Task<bool> DeleteAsync(Guid id, CancellationToken ct);
        Task<bool> LogXApiAsync(Uri verb, RoleEntity role, UserEntity user, CancellationToken ct);

    }

    public class RoleService : IRoleService
    {
        private readonly CiteContext _context;
        private readonly IAuthorizationService _authorizationService;
        private readonly ClaimsPrincipal _user;
        private readonly IMapper _mapper;
         private readonly DatabaseOptions _options;
        private readonly IXApiService _xApiService;

        public RoleService(
            CiteContext context,
            IAuthorizationService authorizationService,
            IPrincipal user,
            IMapper mapper,
            IXApiService xApiService,
            DatabaseOptions options)
        {
            _context = context;
            _authorizationService = authorizationService;
            _user = user as ClaimsPrincipal;
            _mapper = mapper;
            _options = options;
            _xApiService = xApiService;
        }

        public async Task<IEnumerable<ViewModels.Role>> GetByEvaluationAsync(Guid evaluationId, CancellationToken ct)
        {
            if (!(await _authorizationService.AuthorizeAsync(_user, null, new EvaluationUserRequirement(evaluationId, _context))).Succeeded)
                throw new ForbiddenException();

            var roleEntities = await _context.Roles
                .Where(r => r.EvaluationId == evaluationId)
                .Include(r => r.RoleUsers)
                .ThenInclude(ru => ru.User)
                .OrderBy(r => r.Name)
                .ThenBy(r => r.Team.Name)
                .ToListAsync(ct);
            var roles = _mapper.Map<IEnumerable<ViewModels.Role>>(roleEntities).ToList();

            return roles;
        }

        public async Task<IEnumerable<ViewModels.Role>> GetByEvaluationTeamAsync(Guid evaluationId, Guid teamId, CancellationToken ct)
        {
            // must be on the specified Team or an observer for the specified Evaluation
            if (!(await _authorizationService.AuthorizeAsync(_user, null, new TeamUserRequirement(teamId, _context))).Succeeded &&
                !(await _authorizationService.AuthorizeAsync(_user, null, new EvaluationObserverRequirement(evaluationId, _context))).Succeeded
            )
                throw new ForbiddenException();

            var roleEntities = await _context.Roles
                .Where(r => r.EvaluationId == evaluationId &&
                            r.TeamId == teamId)
                .Include(r => r.RoleUsers)
                .ThenInclude(ru => ru.User)
                .OrderBy(r => r.Name)
                .ToListAsync(ct);
            var roles = _mapper.Map<IEnumerable<ViewModels.Role>>(roleEntities).ToList();

            return roles;
        }

        public async Task<ViewModels.Role> GetAsync(Guid id, CancellationToken ct)
        {
            var item = await _context.Roles
                .SingleAsync(a => a.Id == id, ct);

            if (item == null)
                throw new EntityNotFoundException<RoleEntity>();

            // user must be on the requested team or a content developer
            if (
                !(await _authorizationService.AuthorizeAsync(_user, null, new TeamUserRequirement(item.TeamId, _context))).Succeeded &&
                !(await _authorizationService.AuthorizeAsync(_user, null, new ContentDeveloperRequirement())).Succeeded
            )
                throw new ForbiddenException();

            return _mapper.Map<ViewModels.Role>(item);
        }

        public async Task<ViewModels.Role> CreateAsync(ViewModels.Role role, CancellationToken ct)
        {
            // user must be on the requested team or a content developer
            if (
                !(await _authorizationService.AuthorizeAsync(_user, null, new TeamUserRequirement(role.TeamId, _context))).Succeeded &&
                !(await _authorizationService.AuthorizeAsync(_user, null, new ContentDeveloperRequirement())).Succeeded
            )
                throw new ForbiddenException();

            role.Id = role.Id != Guid.Empty ? role.Id : Guid.NewGuid();
            role.DateCreated = DateTime.UtcNow;
            role.CreatedBy = _user.GetId();
            role.DateModified = null;
            role.ModifiedBy = null;
            var roleEntity = _mapper.Map<RoleEntity>(role);

            _context.Roles.Add(roleEntity);
            await _context.SaveChangesAsync(ct);


            return _mapper.Map<ViewModels.Role>(roleEntity);
        }

        public async Task<ViewModels.Role> UpdateAsync(Guid id, ViewModels.Role role, CancellationToken ct)
        {
            // user must be on the requested team or a content developer
            if (
                !(await _authorizationService.AuthorizeAsync(_user, null, new TeamUserRequirement(role.TeamId, _context))).Succeeded &&
                !(await _authorizationService.AuthorizeAsync(_user, null, new ContentDeveloperRequirement())).Succeeded
            )
                throw new ForbiddenException();

            var roleToUpdate = await _context.Roles
                .Include(r => r.RoleUsers)
                .ThenInclude(ru => ru.User)
                .SingleOrDefaultAsync(v => v.Id == id, ct);

            if (roleToUpdate == null)
                throw new EntityNotFoundException<RoleEntity>();

            roleToUpdate.ModifiedBy = _user.GetId();
            roleToUpdate.DateModified = DateTime.UtcNow;
            roleToUpdate.Name = role.Name;
            _context.Roles.Update(roleToUpdate);
            await _context.SaveChangesAsync(ct);

            return _mapper.Map<ViewModels.Role>(roleToUpdate);
        }

        public async Task<ViewModels.Role> AddUserAsync(Guid roleId, Guid userId, CancellationToken ct)
        {
            var roleToUpdate = await _context.Roles
                .Include(r => r.RoleUsers)
                .ThenInclude(ru => ru.User)
                .SingleOrDefaultAsync(v => v.Id == roleId, ct);
            if (roleToUpdate == null)
                throw new EntityNotFoundException<RoleEntity>();

            var userToAdd = await _context.Users.SingleOrDefaultAsync(v => v.Id == userId, ct);
            if (userToAdd == null)
                throw new EntityNotFoundException<UserEntity>();

            // user must be on the requested team
            if (!(await _authorizationService.AuthorizeAsync(_user, null, new TeamUserRequirement(roleToUpdate.TeamId, _context))).Succeeded)
                throw new ForbiddenException();

            var roleUserEntity = new RoleUserEntity()
                {
                    Id = Guid.NewGuid(),
                    RoleId = roleId,
                    UserId = userId
                };
            _context.RoleUsers.Add(roleUserEntity);
            await _context.SaveChangesAsync(ct);

            // create and send xapi statement
            var verb = new Uri("https://w3id.org/xapi/dod-isd/verbs/assigned");
            // object could be the user being added
            await LogXApiAsync(verb, roleToUpdate, userToAdd, ct);

            return _mapper.Map<ViewModels.Role>(roleToUpdate);
        }

        public async Task<ViewModels.Role> RemoveUserAsync(Guid roleId, Guid userId, CancellationToken ct)
        {
            var roleToUpdate = await _context.Roles
                .Include(r => r.RoleUsers)
                .ThenInclude(ru => ru.User)
                .SingleOrDefaultAsync(v => v.Id == roleId, ct);

            if (roleToUpdate == null)
                throw new EntityNotFoundException<RoleEntity>();

            var roleUserToRemove = await _context.RoleUsers.SingleOrDefaultAsync(v => v.RoleId == roleId && v.UserId == userId, ct);
            if (roleUserToRemove == null)
                throw new EntityNotFoundException<UserEntity>();

            // user must be on the requested team
            if (!(await _authorizationService.AuthorizeAsync(_user, null, new TeamUserRequirement(roleToUpdate.TeamId, _context))).Succeeded)
                throw new ForbiddenException();

            _context.RoleUsers.Remove(roleUserToRemove);
            await _context.SaveChangesAsync(ct);

            // create and send xapi statement
            var verb = new Uri ("https://w3id.org/xapi/dod-isd/verbs/removed");
            var userToRemove = await _context.Users.SingleOrDefaultAsync(v => v.Id == userId, ct);
            if (userToRemove == null)
                throw new EntityNotFoundException<UserEntity>();
            // object could be the user being removed
            await LogXApiAsync(verb, roleToUpdate, userToRemove, ct);

            return _mapper.Map<ViewModels.Role>(roleToUpdate);
        }

        public async Task<bool> DeleteAsync(Guid id, CancellationToken ct)
        {
            var roleToDelete = await _context.Roles.SingleOrDefaultAsync(v => v.Id == id, ct);

            if (roleToDelete == null)
                throw new EntityNotFoundException<RoleEntity>();

            // user must be on the requested team or a content developer
            if (
                !(await _authorizationService.AuthorizeAsync(_user, null, new TeamUserRequirement(roleToDelete.TeamId, _context))).Succeeded &&
                !(await _authorizationService.AuthorizeAsync(_user, null, new ContentDeveloperRequirement())).Succeeded
            )
                throw new ForbiddenException();

            _context.Roles.Remove(roleToDelete);
            await _context.SaveChangesAsync(ct);

            return true;
        }
        public async Task<bool> LogXApiAsync(Uri verb, RoleEntity role, UserEntity user, CancellationToken ct)
        {

            if (_xApiService.IsConfigured())
            {
                var evaluation = await _context.Evaluations.Where(e => e.Id == role.EvaluationId).FirstAsync();
                var move = await _context.Moves.Where(m => m.MoveNumber == evaluation.CurrentMoveNumber).FirstAsync();

                var teamId = (await _context.TeamUsers
                    .SingleOrDefaultAsync(tu => tu.UserId == _user.GetId() && tu.Team.EvaluationId == role.EvaluationId)).TeamId;

                var activity = new Dictionary<String,String>();
                activity.Add("id", role.Id.ToString());
                activity.Add("name", role.Name);
                activity.Add("description", "Team-defined role.");
                activity.Add("type", "roles");
                activity.Add("activityType", "http://id.tincanapi.com/activitytype/resource");

                var parent = new Dictionary<String,String>();
                parent.Add("id", evaluation.Id.ToString());
                parent.Add("name", "Evaluation");
                parent.Add("description", evaluation.Description);
                parent.Add("type", "evaluations");
                parent.Add("activityType", "http://adlnet.gov/expapi/activities/simulation");
                parent.Add("moreInfo", "/?evaluation=" + evaluation.Id.ToString());

                var category = new Dictionary<String,String>();

                var grouping = new Dictionary<String,String>();
                grouping.Add("id", move.Id.ToString());
                grouping.Add("name", move.MoveNumber.ToString());
                grouping.Add("description", move.Description);
                grouping.Add("type", "moves");
                grouping.Add("activityType", "http://id.tincanapi.com/activitytype/step");

                var other = new Dictionary<String,String>();
                other.Add("id", user.Id.ToString());
                other.Add("name", user.Name);
                other.Add("description", "The user assigned or removed from the role.");
                other.Add("type", "users");
                other.Add("activityType", "http://id.tincanapi.com/activitytype/user-profile");

                return await _xApiService.CreateAsync(
                    verb, activity, parent, category, grouping, other, teamId, ct);

            }
            return false;
        }

    }

 }

