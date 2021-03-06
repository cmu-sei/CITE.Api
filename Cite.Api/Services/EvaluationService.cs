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
using Cite.Api.Data.Enumerations;
using Cite.Api.Data.Models;
using Cite.Api.Infrastructure.Authorization;
using Cite.Api.Infrastructure.Exceptions;
using Cite.Api.Infrastructure.Extensions;
using Cite.Api.Infrastructure.QueryParameters;
using Cite.Api.ViewModels;

namespace Cite.Api.Services
{
    public interface IEvaluationService
    {
        Task<IEnumerable<ViewModels.Evaluation>> GetAsync(EvaluationGet queryParameters, CancellationToken ct);
        Task<IEnumerable<ViewModels.Evaluation>> GetMineAsync(CancellationToken ct);
        Task<ViewModels.Evaluation> GetAsync(Guid id, CancellationToken ct);
        Task<ViewModels.Evaluation> CreateAsync(ViewModels.Evaluation evaluation, CancellationToken ct);
        Task<ViewModels.Evaluation> UpdateAsync(Guid id, ViewModels.Evaluation evaluation, CancellationToken ct);
        Task<ViewModels.Evaluation> SetCurrentMoveAsync(Guid id, int moveNumber, CancellationToken ct);
        Task<bool> DeleteAsync(Guid id, CancellationToken ct);
    }

    public class EvaluationService : IEvaluationService
    {
        private readonly CiteContext _context;
        private readonly IAuthorizationService _authorizationService;
        private readonly ClaimsPrincipal _user;
        private readonly IMapper _mapper;
        private readonly ISubmissionService _submissionService;
        private readonly ILogger<EvaluationService> _logger;

        public EvaluationService(
            CiteContext context,
            IAuthorizationService authorizationService,
            IPrincipal user,
            IMapper mapper,
            ISubmissionService submissionService,
            ILogger<EvaluationService> logger)
        {
            _context = context;
            _authorizationService = authorizationService;
            _user = user as ClaimsPrincipal;
            _mapper = mapper;
            _submissionService = submissionService;
            _logger = logger;
        }

        public async Task<IEnumerable<ViewModels.Evaluation>> GetAsync(EvaluationGet queryParameters, CancellationToken ct)
        {
            if (!(await _authorizationService.AuthorizeAsync(_user, null, new BaseUserRequirement())).Succeeded)
                throw new ForbiddenException();

            IQueryable<EvaluationEntity> evaluations = null;

            // filter based on user
            if (!String.IsNullOrEmpty(queryParameters.UserId))
            {
                Guid userId;
                Guid.TryParse(queryParameters.UserId, out userId);
                evaluations = _context.Evaluations.Where(sm => sm.CreatedBy == userId);
            }
            // filter based on Scoring Model
            if (!String.IsNullOrEmpty(queryParameters.ScoringModelId))
            {
                Guid scoringModelId;
                Guid.TryParse(queryParameters.ScoringModelId, out scoringModelId);
                if (evaluations == null)
                {
                    evaluations = _context.Evaluations.Where(sm => sm.CreatedBy == scoringModelId);
                }
                else
                {
                    evaluations = evaluations.Where(sm => sm.CreatedBy == scoringModelId);
                }
            }
            // filter based on description
            if (!String.IsNullOrEmpty(queryParameters.Description))
            {
                if (evaluations == null)
                {
                    evaluations = _context.Evaluations.Where(sm => sm.Description.Contains(queryParameters.Description));
                }
                else
                {
                    evaluations = evaluations.Where(sm => sm.Description.Contains(queryParameters.Description));
                }
            }
            else if (evaluations == null)
            {
                evaluations = _context.Evaluations;
            }

            return _mapper.Map<IEnumerable<Evaluation>>(await evaluations.ToListAsync());
        }

        public async Task<IEnumerable<ViewModels.Evaluation>> GetMineAsync(CancellationToken ct)
        {
            if (!(await _authorizationService.AuthorizeAsync(_user, null, new BaseUserRequirement())).Succeeded)
                throw new ForbiddenException();

            var teamIdList =  await _context.TeamUsers
                .Where(tu => tu.UserId == _user.GetId())
                .Select(tu => tu.TeamId)
                .ToListAsync(ct);
            var evaluationList = await _context.EvaluationTeams
                .Where(et => teamIdList.Contains(et.TeamId) && et.Evaluation.Status == ItemStatus.Active)
                .Select(et => et.Evaluation)
                .ToListAsync(ct);

            return _mapper.Map<IEnumerable<Evaluation>>(evaluationList);
        }

        public async Task<ViewModels.Evaluation> GetAsync(Guid id, CancellationToken ct)
        {
            if (!(await _authorizationService.AuthorizeAsync(_user, null, new BaseUserRequirement())).Succeeded)
                throw new ForbiddenException();

            var item = await _context.Evaluations.SingleOrDefaultAsync(sm => sm.Id == id, ct);

            return _mapper.Map<Evaluation>(item);
        }

        public async Task<ViewModels.Evaluation> CreateAsync(ViewModels.Evaluation evaluation, CancellationToken ct)
        {
            if (!(await _authorizationService.AuthorizeAsync(_user, null, new ContentDeveloperRequirement())).Succeeded)
                throw new ForbiddenException();

            evaluation.Id = evaluation.Id != Guid.Empty ? evaluation.Id : Guid.NewGuid();
            evaluation.DateCreated = DateTime.UtcNow;
            evaluation.CreatedBy = _user.GetId();
            evaluation.DateModified = null;
            evaluation.ModifiedBy = null;
            var evaluationEntity = _mapper.Map<EvaluationEntity>(evaluation);

            _context.Evaluations.Add(evaluationEntity);
            await _context.SaveChangesAsync(ct);
            evaluation = await GetAsync(evaluationEntity.Id, ct);

            return evaluation;
        }

        public async Task<ViewModels.Evaluation> UpdateAsync(Guid id, ViewModels.Evaluation evaluation, CancellationToken ct)
        {
            if (!(await _authorizationService.AuthorizeAsync(_user, null, new CanIncrementMoveRequirement())).Succeeded)
                throw new ForbiddenException();

            var evaluationToUpdate = await _context.Evaluations.SingleOrDefaultAsync(v => v.Id == id, ct);

            if (evaluationToUpdate == null)
                throw new EntityNotFoundException<Evaluation>();

            // make sure no evaluation user is on more than one team if setting this evaluation to Active
            if (evaluation.Status == ItemStatus.Active && evaluationToUpdate.Status != ItemStatus.Active)
            {
                // get the teams for this evaluation
                var evaluationTeamList = await _context.EvaluationTeams
                    .Include(et => et.Team)
                    .ThenInclude(t => t.TeamUsers)
                    .ThenInclude(tu => tu.User)
                    .Where(et => et.EvaluationId == evaluation.Id)
                    .Select(et => et.Team)
                    .AsNoTracking()
                    .ToListAsync();
                if (evaluationTeamList.Any())
                {
                    var evaluationTeamUsers = new List<TeamUserEntity>();
                    foreach (var team in evaluationTeamList)
                    {
                        evaluationTeamUsers.AddRange(team.TeamUsers);
                    }
                    var duplicateUserIds = evaluationTeamUsers
                        .GroupBy(tu => tu.UserId)
                        .Where(g => g.Count() > 1)
                        .Select(g => g.Key);
                    var duplicateTeamUsers = evaluationTeamUsers
                        .Where(tu => duplicateUserIds.Contains(tu.UserId))
                        .OrderBy(tu => tu.UserId)
                        .ThenBy(tu => tu.TeamId);
                    if (duplicateTeamUsers.Any())
                    {
                        var message = "This evaluation cannot be set Active, because a user can only be on one team, which is violated by the following:\n";
                        Guid userId = Guid.Empty;
                        foreach (var teamUser in duplicateTeamUsers)
                        {
                            if (teamUser.UserId != userId)
                            {
                                message = message + "\nUser " + teamUser.User.Name + " is on team " + evaluationTeamList.Find(t => t.Id == teamUser.TeamId).Name;
                                userId = teamUser.UserId;
                            }
                            else
                            {
                                message = message + "\n    and is on team " + evaluationTeamList.Find(t => t.Id == teamUser.TeamId).Name;
                            }
                        }
                        throw new ArgumentException(message);
                    }
                }
            }
            // okay to update this evaluation
            evaluation.CreatedBy = evaluationToUpdate.CreatedBy;
            evaluation.DateCreated = evaluationToUpdate.DateCreated;
            evaluation.ModifiedBy = _user.GetId();
            evaluation.DateModified = DateTime.UtcNow;
            _mapper.Map(evaluation, evaluationToUpdate);

            await VerifyOfficialAndTeamSubmissions(evaluationToUpdate, ct);
            _logger.LogDebug("Saving the Evaluation change");
            _context.Evaluations.Update(evaluationToUpdate);
            await _context.SaveChangesAsync(ct);

            evaluation = await GetAsync(evaluationToUpdate.Id, ct);

            return evaluation;
        }

        public async Task<ViewModels.Evaluation> SetCurrentMoveAsync(Guid id, int moveNumber, CancellationToken ct)
        {
            if (!(await _authorizationService.AuthorizeAsync(_user, null, new CanIncrementMoveRequirement())).Succeeded)
                throw new ForbiddenException();

            var evaluationToUpdate = await _context.Evaluations
                .Include(e => e.Moves)
                .SingleOrDefaultAsync(v => v.Id == id, ct);
            if (evaluationToUpdate == null)
                throw new EntityNotFoundException<Evaluation>();
            if (!evaluationToUpdate.Moves.Any(m => m.MoveNumber == moveNumber))
                throw new EntityNotFoundException<Move>();

            await VerifyOfficialAndTeamSubmissions(evaluationToUpdate, ct);
            evaluationToUpdate.ModifiedBy = _user.GetId();
            evaluationToUpdate.DateModified = DateTime.UtcNow;
            evaluationToUpdate.CurrentMoveNumber = moveNumber;

            await _context.SaveChangesAsync(ct);

            var updatedEvaluation = await GetAsync(evaluationToUpdate.Id, ct);

            return updatedEvaluation;
        }

        public async Task<bool> DeleteAsync(Guid id, CancellationToken ct)
        {
            if (!(await _authorizationService.AuthorizeAsync(_user, null, new ContentDeveloperRequirement())).Succeeded)
                throw new ForbiddenException();

            var evaluationToDelete = await _context.Evaluations.SingleOrDefaultAsync(v => v.Id == id, ct);

            if (evaluationToDelete == null)
                throw new EntityNotFoundException<Evaluation>();

            _context.Evaluations.Remove(evaluationToDelete);
            await _context.SaveChangesAsync(ct);

            return true;
        }

        private async Task VerifyOfficialAndTeamSubmissions(EvaluationEntity evaluation, CancellationToken ct)
        {
            // verify all of the official and team submissions for this evaluation
            var submissionList = await _context.Submissions
                .Where(s => s.EvaluationId == evaluation.Id && s.UserId == null)
                .AsNoTracking()
                .ToListAsync();
            // get the teams for this evaluation
            var evaluationTeamList = await _context.EvaluationTeams
                .Include(et => et.Team)
                .ThenInclude(t => t.TeamUsers)
                .ThenInclude(tu => tu.User)
                .Where(et => et.EvaluationId == evaluation.Id)
                .Select(et => et.Team)
                .AsNoTracking()
                .ToListAsync();
            for (var moveNumber=0; moveNumber <= evaluation.CurrentMoveNumber; moveNumber++)
            {
                // make sure all official and team submissions exist
                if (!submissionList.Any(s => s.UserId == null && s.TeamId == null && s.MoveNumber == moveNumber))
                {
                    var submission = new Submission() {
                        Id = Guid.NewGuid(),
                        EvaluationId = evaluation.Id,
                        TeamId = null,
                        UserId = null,
                        ScoringModelId = evaluation.ScoringModelId,
                        MoveNumber = moveNumber
                    };
                    _logger.LogDebug("Make Official submission for move " + moveNumber.ToString());
                    await _submissionService.CreateNewSubmission(_context, submission, ct);
                }
                // team submissions
                foreach (var team in evaluationTeamList)
                {
                    if (!submissionList.Any(s => s.UserId == null && s.TeamId == team.Id && s.MoveNumber == moveNumber))
                    {
                        var submission = new Submission() {
                            Id = Guid.NewGuid(),
                            EvaluationId = evaluation.Id,
                            TeamId = team.Id,
                            UserId = null,
                            ScoringModelId = evaluation.ScoringModelId,
                            MoveNumber = moveNumber
                        };
                        _logger.LogDebug("Make submission for move " + moveNumber + "  team=" + submission.TeamId.ToString());
                        await _submissionService.CreateNewSubmission(_context, submission, ct);
                    }
                }
            }

        }

    }
}

