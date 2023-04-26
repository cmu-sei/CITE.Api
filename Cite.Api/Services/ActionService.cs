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
        Task<bool> LogXApiAsync(Uri verb, ActionEntity action, UserEntity user, CancellationToken ct);
    }

    public class ActionService : IActionService
    {
        private readonly CiteContext _context;
        private readonly IAuthorizationService _authorizationService;
        private readonly ClaimsPrincipal _user;
        private readonly IMapper _mapper;
        private readonly DatabaseOptions _options;
        private readonly IXApiService _xApiService;

        public ActionService(
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

        public async Task<IEnumerable<ViewModels.Action>> GetByEvaluationTeamAsync(Guid evaluationId, Guid teamId, CancellationToken ct)
        {
            // must be on the specified Team or an observer for the specified Evaluation
            if (!(await _authorizationService.AuthorizeAsync(_user, null, new TeamUserRequirement(teamId, _context))).Succeeded &&
                !(await _authorizationService.AuthorizeAsync(_user, null, new EvaluationObserverRequirement(evaluationId, _context))).Succeeded
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
            if (!(await _authorizationService.AuthorizeAsync(_user, null, new TeamUserRequirement(teamId, _context))).Succeeded)
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

            if (!(await _authorizationService.AuthorizeAsync(_user, null, new TeamUserRequirement(item.TeamId, _context))).Succeeded)
                throw new ForbiddenException();

            return _mapper.Map<ViewModels.Action>(item);
        }

        public async Task<ViewModels.Action> CreateAsync(ViewModels.Action action, CancellationToken ct)
        {
            // user must be on the requested team or a content developer
            if (
                !(await _authorizationService.AuthorizeAsync(_user, null, new TeamUserRequirement(action.TeamId, _context))).Succeeded &&
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
            // user must be on the requested team or a content developer
            if (
                !(await _authorizationService.AuthorizeAsync(_user, null, new TeamUserRequirement(action.TeamId, _context))).Succeeded &&
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
            if (!(await _authorizationService.AuthorizeAsync(_user, null, new TeamUserRequirement(actionToUpdate.TeamId, _context))).Succeeded)
                throw new ForbiddenException();

            actionToUpdate.IsChecked = value;
            actionToUpdate.ChangedBy = _user.GetId();
            await _context.SaveChangesAsync(ct);

            // create and send xapi statement
            var verb = new Uri("https://w3id.org/xapi/dod-isd/verbs/completed");
            if (!value) {
                verb = new Uri("https://w3id.org/xapi/dod-isd/verbs/reset");
            }
            //await _xApiService.CreateAsync(verb, actionToUpdate.Description, actionToUpdate.EvaluationId, actionToUpdate.TeamId, ct);
            var user = await _context.Users.SingleOrDefaultAsync(v => v.Id == actionToUpdate.ChangedBy, ct);
            if (user == null)
                throw new EntityNotFoundException<UserEntity>();
            await LogXApiAsync(verb, actionToUpdate, user, ct);

            return _mapper.Map<ViewModels.Action>(actionToUpdate);
        }

        public async Task<bool> DeleteAsync(Guid id, CancellationToken ct)
        {
            var actionToDelete = await _context.Actions.SingleOrDefaultAsync(v => v.Id == id, ct);

            if (actionToDelete == null)
                throw new EntityNotFoundException<ActionEntity>();

            // user must be on the requested team or a content developer
            if (
                !(await _authorizationService.AuthorizeAsync(_user, null, new TeamUserRequirement(actionToDelete.TeamId, _context))).Succeeded &&
                !(await _authorizationService.AuthorizeAsync(_user, null, new ContentDeveloperRequirement())).Succeeded
            )
                throw new ForbiddenException();

            _context.Actions.Remove(actionToDelete);
            await _context.SaveChangesAsync(ct);

            return true;
        }
        public async Task<bool> LogXApiAsync(Uri verb, ActionEntity action, UserEntity user, CancellationToken ct)
        {

            if (_xApiService.IsConfigured())
            {
                //var submissionCategory = _context.SubmissionCategories.Where(sc => sc.Id == submissionOption.SubmissionCategoryId).First();
                //var submission = _context.Submissions.Where(s => s.Id == submissionCategory.SubmissionId).First();
                var evaluation = _context.Evaluations.Where(e => e.Id == action.EvaluationId).First();
                //var scoringCategory = _context.ScoringCategories.Where(sc => sc.Id == submissionCategory.ScoringCategoryId).First();
                //var scoringOption = _context.ScoringOptions.Where(so => so.Id == submissionOption.ScoringOptionId).First();

                var teamId = (await _context.TeamUsers
                    .SingleOrDefaultAsync(tu => tu.UserId == _user.GetId() && tu.Team.EvaluationId == action.EvaluationId)).TeamId;

                // create and send xapi statement

                var activity = new Dictionary<String,String>();

                activity.Add("id", action.Id.ToString());
                activity.Add("name", action.Description);
                activity.Add("description", "Team-defined action or task.");
                activity.Add("type", "action");
                activity.Add("activityType", "http://id.tincanapi.com/activitytype/resource");
                activity.Add("moreInfo", "/action/" + action.Id.ToString());

                var parent = new Dictionary<String,String>();
                parent.Add("id", evaluation.Id.ToString());
                parent.Add("name", "Evaluation");
                parent.Add("description", evaluation.Description);
                parent.Add("type", "Evaluation");
                parent.Add("activityType", "http://adlnet.gov/expapi/activities/simulation");
                parent.Add("moreInfo", "/?evaluation=" + evaluation.Id.ToString());

                var category = new Dictionary<String,String>();
                /*
                category.Add("id", scoringCategory.Id.ToString());
                category.Add("name", scoringCategory.Description);
                category.Add("description", "The scoring category type for the option.");
                category.Add("type", "scoringCategory");
                category.Add("activityType", "http://id.tincanapi.com/activitytype/category");
                category.Add("moreInfo", "");
*/
                // TODO maybe add all scoring categories
                var grouping = new Dictionary<String,String>();
/*
                grouping.Add("id", card.Id.ToString());
                grouping.Add("name", card.Name);
                grouping.Add("description", card.Description);
                grouping.Add("type", "card");
                grouping.Add("activityType", "http://id.tincanapi.com/activitytype/collection-simple");
                grouping.Add("moreInfo", "/?section=archive&exhibit=" + article.ExhibitId.ToString() + "&card=" + card.Id.ToString());
*/
                var other = new Dictionary<String,String>();

                // TODO determine if we should log exhibit as registration
                return await _xApiService.CreateAsync(
                    verb, activity, parent, category, grouping, other, teamId, ct);

            }
            return false;
        }

    }

 }

