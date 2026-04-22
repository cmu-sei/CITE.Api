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
using Cite.Api.Infrastructure.Exceptions;
using Cite.Api.Infrastructure.Extensions;
using Cite.Api.Infrastructure.Options;

namespace Cite.Api.Services
{
    public interface IDutyService
    {
        Task<IEnumerable<ViewModels.Duty>> GetByEvaluationAsync(Guid evaluationId, CancellationToken ct);
        Task<IEnumerable<ViewModels.Duty>> GetByEvaluationTeamAsync(Guid evaluationId, Guid teamId, CancellationToken ct);
        Task<ViewModels.Duty> GetAsync(Guid id, CancellationToken ct);
        Task<ViewModels.Duty> CreateAsync(ViewModels.Duty duty, CancellationToken ct);
        Task<ViewModels.Duty> UpdateAsync(Guid id, ViewModels.Duty duty, CancellationToken ct);
        Task<ViewModels.Duty> AddUserAsync(Guid dutyId, Guid userId, CancellationToken ct);
        Task<ViewModels.Duty> RemoveUserAsync(Guid dutyId, Guid userId, CancellationToken ct);
        Task<bool> DeleteAsync(Guid id, CancellationToken ct);
        Task<bool> LogXApiAsync(Uri verb, DutyEntity duty, UserEntity user, CancellationToken ct);

    }

    public class DutyService : IDutyService
    {
        private readonly CiteContext _context;
        private readonly IAuthorizationService _authorizationService;
        private readonly ClaimsPrincipal _user;
        private readonly IMapper _mapper;
         private readonly DatabaseOptions _options;
        private readonly IXApiService _xApiService;

        public DutyService(
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

        public async Task<IEnumerable<ViewModels.Duty>> GetByEvaluationAsync(Guid evaluationId, CancellationToken ct)
        {
            var dutyEntities = await _context.Duties
                .Where(r => r.EvaluationId == evaluationId)
                .Include(r => r.DutyUsers)
                .ThenInclude(ru => ru.User)
                .OrderBy(r => r.Team.Name)
                .ThenBy(r => r.Name)
                .ToListAsync(ct);
            var duties = _mapper.Map<IEnumerable<ViewModels.Duty>>(dutyEntities).ToList();

            return duties;
        }

        public async Task<IEnumerable<ViewModels.Duty>> GetByEvaluationTeamAsync(Guid evaluationId, Guid teamId, CancellationToken ct)
        {
            var dutyEntities = await _context.Duties
                .Where(r => r.EvaluationId == evaluationId &&
                            r.TeamId == teamId)
                .Include(r => r.DutyUsers)
                .ThenInclude(ru => ru.User)
                .OrderBy(r => r.Name)
                .ToListAsync(ct);
            var duties = _mapper.Map<IEnumerable<ViewModels.Duty>>(dutyEntities).ToList();

            return duties;
        }

        public async Task<ViewModels.Duty> GetAsync(Guid id, CancellationToken ct)
        {
            var item = await _context.Duties
                .SingleAsync(a => a.Id == id, ct);
            if (item == null)
                throw new EntityNotFoundException<DutyEntity>();

            return _mapper.Map<ViewModels.Duty>(item);
        }

        public async Task<ViewModels.Duty> CreateAsync(ViewModels.Duty duty, CancellationToken ct)
        {
            duty.Id = duty.Id != Guid.Empty ? duty.Id : Guid.NewGuid();
            duty.CreatedBy = _user.GetId();
            var dutyEntity = _mapper.Map<DutyEntity>(duty);
            _context.Duties.Add(dutyEntity);
            await _context.SaveChangesAsync(ct);

            return _mapper.Map<ViewModels.Duty>(dutyEntity);
        }

        public async Task<ViewModels.Duty> UpdateAsync(Guid id, ViewModels.Duty duty, CancellationToken ct)
        {
            var dutyToUpdate = await _context.Duties
                .Include(r => r.DutyUsers)
                .ThenInclude(ru => ru.User)
                .SingleOrDefaultAsync(v => v.Id == id, ct);
            if (dutyToUpdate == null)
                throw new EntityNotFoundException<DutyEntity>();

            dutyToUpdate.ModifiedBy = _user.GetId();
            dutyToUpdate.Name = duty.Name;
            _context.Duties.Update(dutyToUpdate);
            await _context.SaveChangesAsync(ct);

            return _mapper.Map<ViewModels.Duty>(dutyToUpdate);
        }

        public async Task<ViewModels.Duty> AddUserAsync(Guid dutyId, Guid userId, CancellationToken ct)
        {
            var dutyToUpdate = await _context.Duties
                .Include(r => r.DutyUsers)
                .ThenInclude(ru => ru.User)
                .SingleOrDefaultAsync(v => v.Id == dutyId, ct);
            if (dutyToUpdate == null)
                throw new EntityNotFoundException<DutyEntity>();

            var userToAdd = await _context.Users.SingleOrDefaultAsync(v => v.Id == userId, ct);
            if (userToAdd == null)
                throw new EntityNotFoundException<UserEntity>();

            var dutyUserEntity = new DutyUserEntity()
                {
                    Id = Guid.NewGuid(),
                    DutyId = dutyId,
                    UserId = userId
                };
            _context.DutyUsers.Add(dutyUserEntity);
            await _context.SaveChangesAsync(ct);
            // create and send xapi statement
            var verb = new Uri("https://w3id.org/xapi/dod-isd/verbs/assigned");
            // object could be the user being added
            await LogXApiAsync(verb, dutyToUpdate, userToAdd, ct);

            return _mapper.Map<ViewModels.Duty>(dutyToUpdate);
        }

        public async Task<ViewModels.Duty> RemoveUserAsync(Guid dutyId, Guid userId, CancellationToken ct)
        {
            var dutyToUpdate = await _context.Duties
                .Include(r => r.DutyUsers)
                .ThenInclude(ru => ru.User)
                .SingleOrDefaultAsync(v => v.Id == dutyId, ct);
            if (dutyToUpdate == null)
                throw new EntityNotFoundException<DutyEntity>();

            var dutyUserToRemove = await _context.DutyUsers.SingleOrDefaultAsync(v => v.DutyId == dutyId && v.UserId == userId, ct);
            if (dutyUserToRemove == null)
                throw new EntityNotFoundException<UserEntity>();

            _context.DutyUsers.Remove(dutyUserToRemove);
            await _context.SaveChangesAsync(ct);
            // create and send xapi statement
            var verb = new Uri ("https://w3id.org/xapi/dod-isd/verbs/removed");
            var userToRemove = await _context.Users.SingleOrDefaultAsync(v => v.Id == userId, ct);
            if (userToRemove == null)
                throw new EntityNotFoundException<UserEntity>();
            // object could be the user being removed
            await LogXApiAsync(verb, dutyToUpdate, userToRemove, ct);

            return _mapper.Map<ViewModels.Duty>(dutyToUpdate);
        }

        public async Task<bool> DeleteAsync(Guid id, CancellationToken ct)
        {
            var dutyToDelete = await _context.Duties.SingleOrDefaultAsync(v => v.Id == id, ct);
            if (dutyToDelete == null)
                throw new EntityNotFoundException<DutyEntity>();

            _context.Duties.Remove(dutyToDelete);
            await _context.SaveChangesAsync(ct);

            return true;
        }
        public async Task<bool> LogXApiAsync(Uri verb, DutyEntity duty, UserEntity user, CancellationToken ct)
        {

            if (_xApiService.IsConfigured())
            {
                var evaluation = await _context.Evaluations.Where(e => e.Id == duty.EvaluationId).FirstAsync();
                var move = await _context.Moves.Where(m => m.MoveNumber == evaluation.CurrentMoveNumber).FirstAsync();

                var teamId = (await _context.TeamMemberships
                    .SingleOrDefaultAsync(tu => tu.UserId == _user.GetId() && tu.Team.EvaluationId == duty.EvaluationId)).TeamId;

                var activity = new Dictionary<String,String>();
                activity.Add("id", duty.Id.ToString());
                activity.Add("name", duty.Name);
                activity.Add("description", "Team-defined duty.");
                activity.Add("type", "duties");
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
                other.Add("description", "The user assigned or removed from the duty.");
                other.Add("type", "users");
                other.Add("activityType", "http://id.tincanapi.com/activitytype/user-profile");

                return await _xApiService.CreateAsync(
                    verb, activity, parent, category, grouping, other, teamId, ct);

            }
            return false;
        }

    }

 }
