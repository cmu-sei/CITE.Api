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
using Cite.Api.Infrastructure.Options;
using Cite.Api.Infrastructure.QueryParameters;
using Cite.Api.ViewModels;
using Cite.Api.Migrations.PostgreSQL.Migrations;

namespace Cite.Api.Services
{
    public interface ISubmissionService
    {
        Task<IEnumerable<ViewModels.Submission>> GetAsync(SubmissionGet queryParameters, CancellationToken ct);
        Task<IEnumerable<ViewModels.Submission>> GetMineByEvaluationAsync(Guid evaluationId, CancellationToken ct);
        Task<IEnumerable<ViewModels.Submission>> GetByEvaluationTeamAsync(Guid evaluationId, Guid teamId, CancellationToken ct);
        Task<ViewModels.Submission> GetAsync(Guid id, CancellationToken ct);
        Task<ViewModels.Submission> CreateAsync(ViewModels.Submission submission, CancellationToken ct);
        Task<SubmissionEntity> CreateNewSubmission(CiteContext citeContext, ViewModels.Submission submission, CancellationToken ct);
        Task<ViewModels.Submission> UpdateAsync(Guid id, ViewModels.Submission submission, CancellationToken ct);
        Task<ViewModels.Submission> SetOptionAsync(Guid id, bool value, CancellationToken ct);
        Task<ViewModels.Submission> ClearSelectionsAsync(Guid id, CancellationToken ct);
        Task<ViewModels.Submission> PresetSelectionsAsync(Guid id, CancellationToken ct);
        Task<bool> DeleteAsync(Guid id, CancellationToken ct);
        Task<ViewModels.Submission> FillTeamAverageAsync(ViewModels.Submission submission, CancellationToken ct);
        Task<ViewModels.Submission> FillTeamTypeAverageAsync(ViewModels.Submission submission, CancellationToken ct);
        Task<ViewModels.Submission> GetTeamAverageAsync(SubmissionEntity submission, CancellationToken ct);
        Task<ViewModels.Submission> GetTypeAverageAsync(Submission submission, CancellationToken ct);
        Task<ViewModels.Submission> AddCommentAsync(Guid submissionId, SubmissionComment submissionComment, CancellationToken ct);
        Task<ViewModels.Submission> UpdateCommentAsync(Guid submissionId, Guid submissionCommentId, SubmissionComment submissionComment, CancellationToken ct);
        Task<ViewModels.Submission> DeleteCommentAsync(Guid submissionId, Guid submissionCommentId, CancellationToken ct);
    }

    public class SubmissionService : ISubmissionService
    {
        private readonly CiteContext _context;
        private readonly IAuthorizationService _authorizationService;
        private readonly ClaimsPrincipal _user;
        private readonly IMapper _mapper;
        private readonly DatabaseOptions _options;
        private readonly ILogger<SubmissionService> _logger;
        private readonly IMoveService _moveService;
        private readonly IXApiService _xApiService;

        public SubmissionService(
            CiteContext context,
            IAuthorizationService authorizationService,
            IPrincipal user,
            IMapper mapper,
            DatabaseOptions options,
            IMoveService moveService,
            IXApiService xApiService,
            ILogger<SubmissionService> logger)
        {
            _context = context;
            _authorizationService = authorizationService;
            _user = user as ClaimsPrincipal;
            _mapper = mapper;
            _options = options;
            _moveService = moveService;
            _xApiService = xApiService;
            _logger = logger;
        }

        public async Task<IEnumerable<ViewModels.Submission>> GetAsync(SubmissionGet queryParameters, CancellationToken ct)
        {
            if (!(await _authorizationService.AuthorizeAsync(_user, null, new BaseUserRequirement())).Succeeded)
                throw new ForbiddenException();

            var userId = Guid.Empty;
            var hasUserId = false;
            var evaluationId = Guid.Empty;
            var hasEvaluationId = false;
            var scoringModelId = Guid.Empty;
            var hasScoringModelId = false;
            var teamId = Guid.Empty;
            var hasTeamId = false;
            // check queryParameters for filter terms
            if ((await _authorizationService.AuthorizeAsync(_user, null, new ContentDeveloperRequirement())).Succeeded)
            {
                // get supplied user ID
                hasUserId = !String.IsNullOrEmpty(queryParameters.UserId);
                if (hasUserId)
                {
                    Guid.TryParse(queryParameters.UserId, out userId);
                }
                // get supplied team ID
                hasTeamId = !String.IsNullOrEmpty(queryParameters.TeamId);
                if (hasTeamId)
                {
                    Guid.TryParse(queryParameters.TeamId, out teamId);
                }
            }
            else
            {
                // filter based on current user
                hasUserId = true;
                userId = _user.GetId();
                // filter based on current user's team
                hasTeamId = true;
                teamId = await _context.TeamUsers.Where(tu => tu.UserId == userId).Select(tu => tu.TeamId).FirstAsync();
            }
            // filter based on evaluation
            hasEvaluationId = !String.IsNullOrEmpty(queryParameters.EvaluationId);
            if (hasEvaluationId)
            {
                Guid.TryParse(queryParameters.EvaluationId, out evaluationId);
            }
            // filter based on scoring model
            hasScoringModelId = !String.IsNullOrEmpty(queryParameters.ScoringModelId);
            if (hasScoringModelId)
            {
                Guid.TryParse(queryParameters.ScoringModelId, out scoringModelId);
            }
            var submissions = _context.Submissions.Where(sm =>
                (!hasUserId || sm.CreatedBy == userId) &&
                (!hasEvaluationId || sm.EvaluationId == evaluationId) &&
                (!hasScoringModelId || sm.ScoringModelId == scoringModelId) &&
                (!hasTeamId || sm.TeamId == teamId));

            return _mapper.Map<IEnumerable<Submission>>(await submissions.ToListAsync());
        }

        public async Task<IEnumerable<ViewModels.Submission>> GetMineByEvaluationAsync(Guid evaluationId, CancellationToken ct)
        {
            if (!(await _authorizationService.AuthorizeAsync(_user, null, new BaseUserRequirement())).Succeeded)
                throw new ForbiddenException();

            var userId = _user.GetId();
            var team = await _context.TeamUsers
                .Where(tu => tu.UserId == userId && tu.Team.EvaluationId == evaluationId)
                .Include(tu => tu.Team.TeamType)
                .Select(tu => tu.Team).FirstAsync();
            var teamId = team.Id;
            var isContributor = team.TeamType.IsOfficialScoreContributor;
            var currentMoveNumber = (await _context.Evaluations.FindAsync(evaluationId)).CurrentMoveNumber;
            var scoringModel = (await _context.Evaluations.Include(e => e.ScoringModel).SingleOrDefaultAsync(e => e.Id == evaluationId)).ScoringModel;
            var submissionEntities = _context.Submissions.Where(sm =>
                (sm.UserId == userId && sm.TeamId == teamId && sm.EvaluationId == evaluationId) ||
                (sm.UserId == null && sm.TeamId == teamId && sm.EvaluationId == evaluationId) ||
                (sm.UserId == null && sm.TeamId == null && sm.EvaluationId == evaluationId && sm.MoveNumber < currentMoveNumber) ||
                (sm.UserId == null && sm.TeamId == null && sm.EvaluationId == evaluationId && sm.MoveNumber == currentMoveNumber && isContributor));
            if (!scoringModel.UseUserScore)
            {
                submissionEntities = submissionEntities.Where(sm => !(sm.UserId == userId));
            }
            if (!scoringModel.UseTeamScore)
            {
                submissionEntities = submissionEntities.Where(sm => !(sm.UserId == null && sm.TeamId == teamId));
            }
            if (!isContributor)
            {
                submissionEntities = submissionEntities.Where(sm => !(sm.UserId == null && sm.TeamId == null && sm.MoveNumber == currentMoveNumber));
            }
            if (!scoringModel.UseOfficialScore)
            {
                submissionEntities = submissionEntities.Where(sm => !(sm.UserId == null && sm.TeamId == null));
            }
            var submissionEntityList = await submissionEntities
                .Include(sm => sm.SubmissionCategories)
                .ThenInclude(sc => sc.SubmissionOptions)
                .ThenInclude(so => so.SubmissionComments)
                .ToListAsync();
            var submissions = _mapper.Map<IEnumerable<Submission>>(submissionEntityList).ToList();
            var averageSubmissions = await GetTeamAndTypeAveragesAsync(evaluationId, team, currentMoveNumber, scoringModel.UseTeamAverageScore, scoringModel.UseTypeAverageScore, ct);
            if (averageSubmissions.Count() > 0)
            {
                submissions.AddRange(averageSubmissions);
            }

            return submissions;
        }

        public async Task<IEnumerable<ViewModels.Submission>> GetByEvaluationTeamAsync(Guid evaluationId, Guid teamId, CancellationToken ct)
        {
            // if the user is on the specified team, call GetMineByEvaluationAsync for normal processing
            if ((await _authorizationService.AuthorizeAsync(_user, null, new TeamUserRequirement(teamId, _context))).Succeeded)
            {
                var mySubmissions = await GetMineByEvaluationAsync(evaluationId, ct);
                return mySubmissions;
            }

            // Otherwise, the user must be an observer to get the specified team's submissions
            if (!(await _authorizationService.AuthorizeAsync(_user, null, new EvaluationObserverRequirement(evaluationId, _context))).Succeeded
            )
                throw new ForbiddenException();

            var userId = _user.GetId();
            var team = await _context.Teams
                .Include(t => t.TeamType)
                .SingleOrDefaultAsync(t => t.Id == teamId);
            var isContributor = team.TeamType.IsOfficialScoreContributor;
            var currentMoveNumber = (await _context.Evaluations.FindAsync(evaluationId)).CurrentMoveNumber;
            var submissionEntities = await _context.Submissions.Where(sm =>
                (sm.UserId == null && sm.TeamId == teamId && sm.EvaluationId == evaluationId) ||
                (sm.UserId == null && sm.TeamId == null && sm.EvaluationId == evaluationId && sm.MoveNumber < currentMoveNumber) ||
                (sm.UserId == null && sm.TeamId == null && sm.EvaluationId == evaluationId && sm.MoveNumber == currentMoveNumber && isContributor))
                .Include(sm => sm.SubmissionCategories)
                .ThenInclude(sc => sc.SubmissionOptions)
                .ThenInclude(so => so.SubmissionComments)
                .ToListAsync();
            var submissions = _mapper.Map<IEnumerable<Submission>>(submissionEntities).ToList();
            if (team.TeamType != null && team.TeamType.ShowTeamTypeAverage)
            {
                var averageSubmissions = await GetTypeAveragesAsync(evaluationId, team, ct);
                averageSubmissions = averageSubmissions.Where(s => !(s.TeamId == teamId && s.ScoreIsAnAverage));
                submissions.AddRange(averageSubmissions);
            }

            return submissions;
        }

        private async Task<IEnumerable<ViewModels.Submission>> GetTeamAndTypeAveragesAsync(
            Guid evaluationId, TeamEntity team, int currentMoveNumber, bool useTeamAverage, bool useTypeAverage, CancellationToken ct)
        {
            var averageSubmissions = new List<Submission>();
            // calculate the average of users on the team
            var submissionEntities = await _context.Submissions.Where(sm =>
                (sm.UserId != null && sm.TeamId == team.Id && sm.EvaluationId == evaluationId)).ToListAsync(ct);
            for (var move = 0; move <= currentMoveNumber; move ++)
            {
                var moveSubmissions = submissionEntities.Where(s => s.MoveNumber == move).ToList();
                if (useTeamAverage)
                {
                    var teamAverageSubmission = CreateAverageSubmission(moveSubmissions);
                    if (teamAverageSubmission != null)
                    {
                        teamAverageSubmission.Id = Guid.NewGuid();
                        teamAverageSubmission.UserId = null;
                        teamAverageSubmission.TeamId = team.Id;
                        teamAverageSubmission.GroupId = team.TeamTypeId;
                        teamAverageSubmission.MoveNumber = move;
                        averageSubmissions.Add(teamAverageSubmission);
                    }
                }
            }
            if (useTypeAverage && team.TeamType != null && team.TeamType.ShowTeamTypeAverage)
            {
                averageSubmissions.AddRange(await GetTypeAveragesAsync(evaluationId, team, ct));
            }

            return averageSubmissions;
        }

        private async Task<IEnumerable<ViewModels.Submission>> GetTypeAveragesAsync(
            Guid evaluationId, TeamEntity team, CancellationToken ct)
        {
            var currentMoveNumber = (await _context.Evaluations.FindAsync(evaluationId)).CurrentMoveNumber;
            var averageSubmissions = new List<Submission>();
            // get the submission entities
            var submissionEntities = await _context.Submissions.Where(sm =>
                (sm.UserId != null && sm.TeamId == team.Id && sm.EvaluationId == evaluationId)).ToListAsync(ct);
            for (var move = 0; move <= currentMoveNumber; move ++)
            {
                var moveSubmissions = submissionEntities.Where(s => s.MoveNumber == move).ToList();
            }
            if (team.TeamType != null && team.TeamType.IsOfficialScoreContributor)
            {
                // calculate the average of teams in the team type
                var teamIds = await _context.Teams.Where(t => t.TeamTypeId == team.TeamTypeId).Select(t => t.Id).ToListAsync(ct);
                submissionEntities = await _context.Submissions.Where(sm =>
                    (sm.UserId == null && teamIds.Contains((Guid)sm.TeamId) && sm.EvaluationId == evaluationId)).ToListAsync(ct);
                for (var move = 0; move <= currentMoveNumber; move ++)
                {
                    var moveSubmissions = submissionEntities.Where(s => s.MoveNumber == move).ToList();
                    var teamTypeAverageSubmission = CreateAverageSubmission(moveSubmissions);
                    if (teamTypeAverageSubmission != null)
                    {
                        teamTypeAverageSubmission.Id = Guid.NewGuid();
                        teamTypeAverageSubmission.UserId = null;
                        teamTypeAverageSubmission.TeamId = null;
                        teamTypeAverageSubmission.GroupId = team.TeamTypeId;
                        teamTypeAverageSubmission.MoveNumber = move;
                        averageSubmissions.Add(teamTypeAverageSubmission);
                    }
                }
            }

            return averageSubmissions;
        }

        public async Task<ViewModels.Submission> FillTeamAverageAsync(ViewModels.Submission submission, CancellationToken ct)
        {
            if (!(await _authorizationService.AuthorizeAsync(_user, null, new TeamUserRequirement((Guid)submission.TeamId, _context))).Succeeded)
                throw new ForbiddenException();
            if (!submission.ScoreIsAnAverage || submission.TeamId == null || submission.UserId != null)
                throw new ArgumentException("The submission must be a team average submission.");

            return await GetTeamAverageAsync(_mapper.Map<SubmissionEntity>(submission), ct);
        }

        public async Task<ViewModels.Submission> FillTeamTypeAverageAsync(ViewModels.Submission submission, CancellationToken ct)
        {
            if (!(await _authorizationService.AuthorizeAsync(_user, null, new BaseUserRequirement())).Succeeded)
                throw new ForbiddenException();
            if (!submission.ScoreIsAnAverage || submission.TeamId != null || submission.UserId != null || submission.GroupId == null)
                throw new ArgumentException("The submission must be a teamType average submission.");

            var teamType = await _context.TeamTypes.SingleOrDefaultAsync(tt => tt.Id == submission.GroupId);
            if (!teamType.ShowTeamTypeAverage)
            {
                throw new ForbiddenException("TeamType " + teamType.Name + " cannot view the average score for the TeamType.");
            }
            var isObserver = (await _authorizationService.AuthorizeAsync(_user, null, new EvaluationObserverRequirement(submission.EvaluationId, _context))).Succeeded;            
            var userId = _user.GetId();
            var teamIdList = await _context.Teams.Where(t => t.EvaluationId == submission.EvaluationId && t.TeamTypeId == submission.GroupId).Select(t => t.Id).ToListAsync(ct);
            var canSeeTeamTypeAverage = await _context.TeamUsers.Where(tu => teamIdList.Contains(tu.TeamId) && tu.UserId == userId).AnyAsync(ct);
            if (!canSeeTeamTypeAverage && !isObserver)
                throw new ForbiddenException("Your TeamType does not have the permission to view the TeamType average score.");

            return await GetTypeAverageAsync(submission, ct);
        }

        public async Task<ViewModels.Submission> GetTeamAverageAsync(SubmissionEntity submission, CancellationToken ct)
        {
            if (submission.TeamId == null)
            {
                return null;
            }
            var move = submission.MoveNumber;
            // calculate the average of users on the team
            var teamUserSubmissions = await _context.Submissions
                .Where(sm =>(sm.UserId != null && sm.TeamId == submission.TeamId && sm.EvaluationId == submission.EvaluationId && sm.MoveNumber == move))
                .Include(s => s.SubmissionCategories)
                .ThenInclude(sc => sc.SubmissionOptions)
                .ToListAsync(ct);
            var teamAverageSubmission = CreateAverageSubmission(teamUserSubmissions);
            if (teamAverageSubmission != null)
            {
                teamAverageSubmission.Id = Guid.NewGuid();
                teamAverageSubmission.UserId = null;
                teamAverageSubmission.TeamId = submission.TeamId;
                teamAverageSubmission.MoveNumber = move;
                var teamTypeId = (await _context.Teams.FindAsync(submission.TeamId))?.TeamTypeId;
                teamAverageSubmission.GroupId = teamTypeId;
            }

            return teamAverageSubmission;
        }

        public async Task<Submission> GetTypeAverageAsync(Submission submission, CancellationToken ct)
        {
            var teamType = await _context.TeamTypes.SingleOrDefaultAsync(tt => tt.Id == submission.GroupId);
            // calculate the average of teams in the team type
            var teamIds = await _context.Teams
                .Where(t => t.EvaluationId == submission.EvaluationId && t.TeamTypeId == teamType.Id)
                .Select(t => t.Id)
                .ToListAsync(ct);
            var teamTypeSubmissions = await _context.Submissions
                .Where(sm => sm.UserId == null && sm.TeamId != null && teamIds.Contains((Guid)sm.TeamId) && sm.EvaluationId == submission.EvaluationId && sm.MoveNumber == submission.MoveNumber)
                .Include(s => s.SubmissionCategories)
                .ThenInclude(sc => sc.SubmissionOptions)
                .ToListAsync(ct);
            var teamTypeAverageSubmission = CreateAverageSubmission(teamTypeSubmissions);
            if (teamTypeAverageSubmission != null)
            {
                teamTypeAverageSubmission.Id = Guid.NewGuid();
                teamTypeAverageSubmission.UserId = null;
                teamTypeAverageSubmission.TeamId = null;
                teamTypeAverageSubmission.GroupId = teamType.Id;
                teamTypeAverageSubmission.MoveNumber = submission.MoveNumber;
            }
            return teamTypeAverageSubmission;
        }

        public async Task<ViewModels.Submission> GetAsync(Guid id, CancellationToken ct)
        {
            if (!(await _authorizationService.AuthorizeAsync(_user, null, new BaseUserRequirement())).Succeeded)
                throw new ForbiddenException();

            var item = await _context.Submissions
                .Include(sm => sm.SubmissionCategories)
                .ThenInclude(sc => sc.SubmissionOptions)
                .ThenInclude(so => so.SubmissionComments)
                .SingleAsync(sm => sm.Id == id, ct);
            if (item == null) 
                throw new EntityNotFoundException<Submission>("Submission not found " + id.ToString());
            // verify permission for this object
            if (!(await _authorizationService.AuthorizeAsync(_user, null, new ContentDeveloperRequirement())).Succeeded &&
                !(await _authorizationService.AuthorizeAsync(_user, null, new EvaluationUserRequirement((Guid)item.EvaluationId, _context))).Succeeded)
                throw new ForbiddenException("Access to submission " + item.Id + " was denied.");

            var userId = _user.GetId();
            var evaluationTeamIdList = await _context.Teams
                .Where(t => t.EvaluationId == item.EvaluationId)
                .Select(t => t.Id)
                .ToListAsync();
            var team = await _context.TeamUsers
                .Where(tu => tu.UserId == userId && evaluationTeamIdList.Contains(tu.TeamId))
                .Include(tu => tu.Team.TeamType)
                .Select(tu => tu.Team).FirstOrDefaultAsync();
            var teamId = team.Id;
            // Observers can view all team scores, others can only view their own team
            if (!(await _authorizationService.AuthorizeAsync(_user, null, new EvaluationObserverRequirement((Guid)item.EvaluationId, _context))).Succeeded)
            {
                evaluationTeamIdList = new List<Guid>{team.Id};
            }
            var isCollaborator = team.TeamType.IsOfficialScoreContributor;
            var currentMoveNumber = (await _context.Evaluations.FindAsync(item.EvaluationId)).CurrentMoveNumber;
            var isIncrementer = (await _authorizationService.AuthorizeAsync(_user, null, new CanIncrementMoveRequirement((Guid)item.EvaluationId, _context))).Succeeded;
            var hasAccess =
                (item.UserId == userId && item.TeamId == teamId && item.EvaluationId == item.EvaluationId) ||
                (item.UserId == null && (item.TeamId != null && evaluationTeamIdList.Contains((Guid)item.TeamId)) && item.EvaluationId == item.EvaluationId) ||
                (item.UserId == null && item.TeamId == null && item.EvaluationId == item.EvaluationId && item.MoveNumber < currentMoveNumber) ||
                (item.UserId == null && item.TeamId == null && item.EvaluationId == item.EvaluationId && item.MoveNumber == currentMoveNumber && (isCollaborator || isIncrementer));
            if (!hasAccess)
                throw new ForbiddenException("Access to submission " + item.Id + " was denied.  TeamId was " + item.TeamId.ToString() + ".");

            return _mapper.Map<Submission>(item);
        }

        public async Task<ViewModels.Submission> CreateAsync(ViewModels.Submission submission, CancellationToken ct)
        {
            _logger.LogDebug("Making create request: Move=" + submission.MoveNumber + " user=" + submission.UserId + " team=" + submission.TeamId + " evaluation=" + submission.EvaluationId);
            var userId = _user.GetId();
            // 1. An evaluationId must be supplied
            if (submission.EvaluationId == Guid.Empty)
                throw new ArgumentException("An Evaluation ID must be supplied to create a new submission");
            // 2. the calling user must be a user on this evaluation.
            if (!(await _authorizationService.AuthorizeAsync(_user, null, new EvaluationUserRequirement(submission.EvaluationId, _context))).Succeeded)
                throw new ForbiddenException();
            var evaluationTeamIdList = await _context.Teams
                .Where(t => t.EvaluationId == submission.EvaluationId)
                .Select(t => t.Id)
                .ToListAsync();
            // if submission.userId is supplied
            if (submission.UserId != null)
            {
                // 3. submission.userId must match the current user
                if ((Guid)submission.UserId != userId)
                    throw new ForbiddenException("You are not able to create a submission for user " + submission.UserId + ".");
                // 4. a team ID must also be supplied
                if (submission.TeamId == null)
                    throw new ForbiddenException("A Team ID was not supplied for user " + submission.UserId + ".");
            }
            // if a teamId is supplied
            else if (submission.TeamId != null)
            {
                // for a team submission, the user must be an evaluation observer or be a member of the team
                if (!(await _authorizationService.AuthorizeAsync(_user, null, new EvaluationObserverRequirement(submission.EvaluationId, _context))).Succeeded)
                {
                    var teamUserEntity = await _context.TeamUsers
                        .SingleOrDefaultAsync(tu => tu.TeamId == submission.TeamId && tu.UserId == userId);
                    // 5. the user is not on a required team
                    if (teamUserEntity == null)
                        throw new ForbiddenException("The requested user is not a member of the requested team.");
                }
            }
            // Create requested submission
            var requestedSubmissionEntity = await createRequestedSubmissionAndOthers(submission, ct);
            if (requestedSubmissionEntity == null)
            {
                _logger.LogDebug("Requested submission was created before this call could create it");
                // find the requested submission entity
                requestedSubmissionEntity = await _context.Submissions.FirstOrDefaultAsync(s =>
                    s.UserId == submission.UserId &&
                    s.TeamId == submission.TeamId &&
                    s.EvaluationId == submission.EvaluationId &&
                    s.MoveNumber == submission.MoveNumber
                );
            }

            // create and send xapi statement
            var verb = "initialized";
            await _xApiService.CreateAsync(verb, requestedSubmissionEntity.ScoringModel.Description, requestedSubmissionEntity.EvaluationId.Value, requestedSubmissionEntity.TeamId.Value, ct);

            return _mapper.Map<ViewModels.Submission>(requestedSubmissionEntity);
        }

        public async Task<ViewModels.Submission> UpdateAsync(Guid id, ViewModels.Submission submission, CancellationToken ct)
        {
            var isOnTeam = await _context.TeamUsers.AnyAsync(tu => tu.UserId == _user.GetId() && tu.TeamId == submission.TeamId);
            if (!(
                    ((await _authorizationService.AuthorizeAsync(_user, null, new CanIncrementMoveRequirement(submission.EvaluationId, _context))).Succeeded
                        && submission.UserId == null
                        && (submission.TeamId == null || isOnTeam)) ||
                    ((await _authorizationService.AuthorizeAsync(_user, null, new CanSubmitRequirement(submission.EvaluationId, _context))).Succeeded && isOnTeam) ||
                    ((await _authorizationService.AuthorizeAsync(_user, null, new BaseUserRequirement())).Succeeded && submission.UserId == _user.GetId())
            ))
                throw new ForbiddenException();

            var submissionToUpdate = await _context.Submissions.SingleOrDefaultAsync(v => v.Id == id, ct);

            if (submissionToUpdate == null)
                throw new EntityNotFoundException<Submission>();

            submission.CreatedBy = submissionToUpdate.CreatedBy;
            submission.DateCreated = submissionToUpdate.DateCreated;
            submission.ModifiedBy = _user.GetId();
            submission.DateModified = DateTime.UtcNow;
            _mapper.Map(submission, submissionToUpdate);

            _context.Submissions.Update(submissionToUpdate);
            await _context.SaveChangesAsync(ct);

            submission = await GetAsync(submissionToUpdate.Id, ct);

            // create and send xapi statement
            var verb = "updated";
            if (submission.Status == Data.Enumerations.ItemStatus.Complete) {
                verb = "submitted";
            }
            await _xApiService.CreateAsync(verb, submission.Score.ToString(), submission.EvaluationId, submission.TeamId.Value, ct);

            return submission;
        }

        public async Task<ViewModels.Submission> SetOptionAsync(Guid id, bool value, CancellationToken ct)
        {
            var submissionOptionToUpdate = await _context.SubmissionOptions
                .Include(so => so.ScoringOption).SingleAsync(v => v.Id == id, ct);

            if (submissionOptionToUpdate == null)
                throw new EntityNotFoundException<SubmissionOption>();

            var submissionCategoryEntity = await _context.SubmissionCategories.FindAsync(submissionOptionToUpdate.SubmissionCategoryId);
            var submissionEntity = await _context.Submissions.FindAsync(submissionCategoryEntity.SubmissionId);
            var isOnTeam = await _context.TeamUsers.AnyAsync(tu => tu.UserId == _user.GetId() && tu.TeamId == submissionEntity.TeamId);
            var evaluationId = (Guid)submissionEntity.EvaluationId;
            if (!(await HasOptionAccess(submissionEntity, ct)))
                throw new ForbiddenException();

            // get modified date/time
            var modifiedDateTime = DateTime.UtcNow;
            var modifiedBy = _user.GetId();
            // Only one Modifier can be selected
            if (submissionOptionToUpdate.ScoringOption.IsModifier && value)
            {
                var submissionOptionsToClear = _context.SubmissionOptions.Include(so => so.ScoringOption).Where(so =>
                    so.SubmissionCategoryId == submissionCategoryEntity.Id  && so.ScoringOption.IsModifier && so.IsSelected);
                foreach (var submissionOption in submissionOptionsToClear)
                {
                    submissionOption.IsSelected = false;
                    submissionOption.ModifiedBy = modifiedBy;
                    submissionOption.DateModified = modifiedDateTime;
                }
            }
            else
            {
                // Only one option can be selected if selectMultiple is not true
                var scoringOptionSelection = (await _context.SubmissionCategories
                    .Include(sc => sc.ScoringCategory)
                    .Where(so => so.Id == submissionCategoryEntity.Id)
                    .Select(so => so.ScoringCategory.ScoringOptionSelection)
                    .FirstAsync());
                if (scoringOptionSelection != Data.Enumerations.ScoringOptionSelection.Multiple)
                {
                    var submissionOptionsToClear = _context.SubmissionOptions.Where(so =>
                        so.SubmissionCategoryId == submissionCategoryEntity.Id  && so.IsSelected);
                    foreach (var submissionOption in submissionOptionsToClear)
                    {
                        submissionOption.IsSelected = false;
                        submissionOption.ModifiedBy = modifiedBy;
                        submissionOption.DateModified = modifiedDateTime;
                    }
                }
            }
            // update submission option
            submissionOptionToUpdate.IsSelected = value;
            submissionOptionToUpdate.ModifiedBy = modifiedBy;
            submissionOptionToUpdate.DateModified = modifiedDateTime;
            // update submission
            submissionEntity.ModifiedBy = modifiedBy;
            submissionEntity.DateModified = modifiedDateTime;
            await _context.SaveChangesAsync(ct);
            submissionEntity = await UpdateScoreAsync(ct, submissionEntity.Id);

            return _mapper.Map<Submission>(submissionEntity);
        }

        private async Task<SubmissionEntity> createRequestedSubmissionAndOthers(ViewModels.Submission submission, CancellationToken ct)
        {
            var requestedUserId = submission.UserId;
            var requestedTeamId = submission.TeamId;
            // get the requested and maximum move numbers
            var requestedMoveNumber = submission.MoveNumber;
            // maxMoveNumber is the current move
            var maxMoveNumber = await _context.Evaluations
                .Where(e => e.Id == submission.EvaluationId)
                .Select(e => e.CurrentMoveNumber)
                .MaxAsync(ct);
            // get the current user/team/evaluation submissions.
            // user submissions are only returned if the submission UserId is equal to the current user's ID
            var submissionEntityList = await _context.Submissions.Where(s =>
                s.EvaluationId == submission.EvaluationId &&
                ((s.UserId == submission.UserId && submission.UserId == _user.GetId()) || s.UserId == null) &&
                (s.TeamId == submission.TeamId || s.TeamId == null)
            ).ToListAsync(ct);
            // get a list of moves for the evaluation
            var moves = await _moveService.GetByEvaluationAsync(submission.EvaluationId, ct);
            // verify submissions for previous moves, if the requested submission is for the current user
            if (submission.UserId == _user.GetId() || submission.UserId == null)
            {
                foreach (var move in moves)
                {
                    if (move.MoveNumber <= maxMoveNumber)
                    {
                    if (!submissionEntityList.Any(s => s.MoveNumber == move.MoveNumber &&
                        (s.UserId == submission.UserId)))
                        {
                            submission.MoveNumber = move.MoveNumber;
                            await CreateNewSubmission(_context, submission, ct);
                        }
                    }
                }
            }
            // return the requested submission
            var submissionEntity = await _context.Submissions.FirstOrDefaultAsync(s =>
                s.MoveNumber == requestedMoveNumber && s.UserId == requestedUserId && s.TeamId == requestedTeamId && s.EvaluationId == submission.EvaluationId);

            return submissionEntity;
        }

        public async Task<SubmissionEntity> CreateNewSubmission(CiteContext citeContext, ViewModels.Submission submission, CancellationToken ct)
        {
            // actually create a new submission
            submission.Id = Guid.NewGuid();
            submission.DateCreated = DateTime.UtcNow;
            submission.CreatedBy = _user.GetId();
            submission.DateModified = null;
            submission.ModifiedBy = null;
            submission.Status = Data.Enumerations.ItemStatus.Active;
            var submissionEntity = _mapper.Map<SubmissionEntity>(submission);
            citeContext.Submissions.Add(submissionEntity);
            // catch race condition if we try to add the same submission twice
            try
            {
                await citeContext.SaveChangesAsync(ct);
                var scoringModelEntity = await citeContext.ScoringModels
                    .Include(sm => sm.ScoringCategories)
                    .ThenInclude(sc => sc.ScoringOptions)
                    .FirstAsync(sm => sm.Id == submissionEntity.ScoringModelId);
                await CreateSubmissionCategories(submissionEntity, scoringModelEntity, ct);
                //_logger.LogDebug("*** Created a submission");
                _logger.LogInformation("Created submission for move " + submission.MoveNumber.ToString());

                return submissionEntity;
            }
            catch (System.Exception)
            {
                _logger.LogWarning("!!! Tried to create a duplicate submission");
                return null;
            }
        }

        private async Task<SubmissionEntity> UpdateScoreAsync(CancellationToken ct, Guid submissionId)
        {
            var categoryScores = new List<CategoryScore>();
            var scoringModelId = await _context.Submissions.Where(s => s.Id == submissionId).Select(s => s.ScoringModelId).FirstAsync();
            var submissionCategories = _context.SubmissionCategories
                .Where(sc => sc.SubmissionId == submissionId)
                .Include(sc => sc.SubmissionOptions)
                .ThenInclude(so => so.ScoringOption)
                .Include(sc => sc.SubmissionOptions)
                .ThenInclude(so => so.SubmissionComments)
                .Include(sc => sc.ScoringCategory);
            foreach (var submissionCategory in submissionCategories)
            {
                if (!String.IsNullOrWhiteSpace(submissionCategory.ScoringCategory.CalculationEquation))
                {
                    var categoryScore = CalculateCategoryScore(submissionCategory);
                    submissionCategory.Score = categoryScore.ActualScore;
                    categoryScores.Add(categoryScore);
                }
            }
            await _context.SaveChangesAsync(ct);
            var submissionEntity = await _context.Submissions.Include(s => s.ScoringModel).FirstAsync(s => s.Id == submissionId, ct);
            submissionEntity.Score = CalculateSubmissionScore(submissionEntity.ScoringModel.CalculationEquation, categoryScores);
            await _context.SaveChangesAsync(ct);

            // create and send xapi statement
            var verb = "scored"; // could be interacted
            await _xApiService.CreateAsync(verb, submissionEntity.ScoringModel.Description, submissionEntity.EvaluationId.Value, submissionEntity.TeamId.Value, ct);

            return submissionEntity;
        }

        public async Task<ViewModels.Submission> ClearSelectionsAsync(Guid id, CancellationToken ct)
        {
            var submissionToClear = await _context.Submissions
                .Include(s => s.SubmissionCategories)
                .ThenInclude(sc => sc.SubmissionOptions)
                .ThenInclude(so => so.SubmissionComments)
                .FirstAsync(v => v.Id == id);

            if (submissionToClear == null)
                throw new EntityNotFoundException<Submission>();

            var isOnTeam = await _context.TeamUsers.AnyAsync(tu => tu.UserId == _user.GetId() && tu.TeamId == submissionToClear.TeamId);
            var evaluationId = (Guid)submissionToClear.EvaluationId;
            if (!(await HasOptionAccess(submissionToClear, ct)))
                throw new ForbiddenException();

            if (submissionToClear.Status != Data.Enumerations.ItemStatus.Active)
                throw new Exception($"Cannot clear selections of a submission ({submissionToClear.Id}) that is not currently active.");

            foreach (var submissionCategory in submissionToClear.SubmissionCategories)
            {
                foreach (var submissionOption in submissionCategory.SubmissionOptions)
                {
                    if (submissionOption.IsSelected) {
                        submissionOption.IsSelected = false;
                        submissionOption.ModifiedBy = _user.GetId();
                        submissionOption.DateModified = DateTime.UtcNow;
                    }
                    foreach(var submissionComment in submissionOption.SubmissionComments)
                    {
                    _context.SubmissionComments.Remove(submissionComment);
                    }
                }
                submissionCategory.Score = 0.0;
            };
            submissionToClear.Score = 0.0;
            _context.Submissions.Update(submissionToClear);
            await _context.SaveChangesAsync(ct);
            var submission = await GetAsync(id, ct);

            // create and send xapi statement
            var verb = "cleared"; // could be interacted or initialized
            await _xApiService.CreateAsync(verb, submissionToClear.Score.ToString(), submissionToClear.EvaluationId.Value, submissionToClear.TeamId.Value, ct);

            return submission;
        }

        public async Task<ViewModels.Submission> PresetSelectionsAsync(Guid id, CancellationToken ct)
        {
            var targetSubmission = await _context.Submissions
                .Include(s => s.SubmissionCategories)
                .ThenInclude(sc => sc.SubmissionOptions)
                .ThenInclude(so => so.ScoringOption)
                .Include(s => s.SubmissionCategories)
                .ThenInclude(sc => sc.ScoringCategory)
                .FirstAsync(v => v.Id == id);

            if (targetSubmission == null)
                throw new EntityNotFoundException<Submission>();

            var isOnTeam = await _context.TeamUsers.AnyAsync(tu => tu.UserId == _user.GetId() && tu.TeamId == targetSubmission.TeamId);
            var evaluationId = (Guid)targetSubmission.EvaluationId;
            if (!(await HasOptionAccess(targetSubmission, ct)))
                throw new ForbiddenException();

            if (targetSubmission.Status != Data.Enumerations.ItemStatus.Active)
                throw new Exception($"Cannot preset selections of a submission ({targetSubmission.Id}) that is not currently active.");

            var baseSubmission = await  _context.Submissions
                .Include(s => s.SubmissionCategories)
                .ThenInclude(sc => sc.SubmissionOptions)
                .FirstOrDefaultAsync(b => b.EvaluationId == targetSubmission.EvaluationId
                    && b.UserId == targetSubmission.UserId
                    && b.TeamId == targetSubmission.TeamId
                    && b.MoveNumber == targetSubmission.MoveNumber - 1);
            if (baseSubmission != null)
            {
                targetSubmission.Score = baseSubmission.Score;
                foreach (var targetSubmissionCategory in targetSubmission.SubmissionCategories)
                {
                    var baseSubmissionCategory = baseSubmission.SubmissionCategories.First(sc => sc.ScoringCategoryId == targetSubmissionCategory.ScoringCategoryId);
                    foreach (var submissionOption in targetSubmissionCategory.SubmissionOptions)
                    {
                        var baseSubmissionOption = baseSubmissionCategory.SubmissionOptions.First(so => so.ScoringOptionId == submissionOption.ScoringOptionId);
                        if (submissionOption.IsSelected != baseSubmissionOption.IsSelected) {
                            submissionOption.IsSelected = baseSubmissionOption.IsSelected;
                            submissionOption.ModifiedBy = _user.GetId();
                            submissionOption.DateModified = DateTime.UtcNow;
                        }
                    }
                    targetSubmissionCategory.Score = baseSubmissionCategory.Score;
                };
                _context.Submissions.Update(targetSubmission);
                await _context.SaveChangesAsync(ct);
            }
            var submission = await GetAsync(id, ct);

            // create and send xapi statement
            var verb = "preset"; // could be interacted
            await _xApiService.CreateAsync(verb, targetSubmission.Score.ToString(), targetSubmission.EvaluationId.Value, targetSubmission.TeamId.Value, ct);

            return submission;
        }

        public async Task<bool> DeleteAsync(Guid id, CancellationToken ct)
        {
            if (!(await _authorizationService.AuthorizeAsync(_user, null, new ContentDeveloperRequirement())).Succeeded)
                throw new ForbiddenException();

            var submissionToDelete = await _context.Submissions.SingleOrDefaultAsync(v => v.Id == id, ct);

            if (submissionToDelete == null)
                throw new EntityNotFoundException<Submission>();

            _context.Submissions.Remove(submissionToDelete);
            await _context.SaveChangesAsync(ct);

            return true;
        }

        public async Task<ViewModels.Submission> AddCommentAsync(Guid submissionId, SubmissionComment submissionComment, CancellationToken ct)
        {
            var submissionEntity = await _context.Submissions.SingleOrDefaultAsync(v => v.Id == submissionId, ct);
            if (submissionEntity == null)
                throw new EntityNotFoundException<Submission>();

            if (!(await HasOptionAccess(submissionEntity, ct)))
                throw new ForbiddenException();
            // Add the submission comment
            submissionComment.Id = submissionComment.Id != Guid.Empty ? submissionComment.Id : Guid.NewGuid();
            submissionComment.DateCreated = DateTime.UtcNow;
            submissionComment.CreatedBy = _user.GetId();
            submissionComment.DateModified = null;
            submissionComment.ModifiedBy = null;
            var submissionCommentEntity = _mapper.Map<SubmissionCommentEntity>(submissionComment);
            _context.SubmissionComments.Add(submissionCommentEntity);
            // update and return the submission
            submissionEntity.DateModified = submissionComment.DateCreated;
            submissionEntity.ModifiedBy = submissionComment.CreatedBy;
            await _context.SaveChangesAsync(ct);
            
            return await GetAsync(submissionId, ct);
        }

        public async Task<ViewModels.Submission> UpdateCommentAsync(Guid submissionId, Guid submissionCommentId, SubmissionComment submissionComment, CancellationToken ct)
        {
            var submissionEntity = await _context.Submissions.SingleOrDefaultAsync(v => v.Id == submissionId, ct);
            if (submissionEntity == null)
                throw new EntityNotFoundException<Submission>();

            if (!(await HasOptionAccess(submissionEntity, ct)))
                throw new ForbiddenException();

            var submissionCommentToUpdate = await _context.SubmissionComments.SingleOrDefaultAsync(v => v.Id == submissionCommentId, ct);

            if (submissionCommentToUpdate == null)
                throw new EntityNotFoundException<SubmissionComment>();

            submissionComment.CreatedBy = submissionCommentToUpdate.CreatedBy;
            submissionComment.DateCreated = submissionCommentToUpdate.DateCreated;
            submissionComment.ModifiedBy = _user.GetId();
            submissionComment.DateModified = DateTime.UtcNow;
            _mapper.Map(submissionComment, submissionCommentToUpdate);
            _context.SubmissionComments.Update(submissionCommentToUpdate);
            // update and return the submission
            submissionEntity.DateModified = submissionComment.DateCreated;
            submissionEntity.ModifiedBy = submissionComment.CreatedBy;
            await _context.SaveChangesAsync(ct);
            
            return await GetAsync(submissionId, ct);
        }

        public async Task<ViewModels.Submission> DeleteCommentAsync(Guid submissionId, Guid submissionCommentId, CancellationToken ct)
        {
            var submissionEntity = await _context.Submissions.SingleOrDefaultAsync(v => v.Id == submissionId, ct);
            if (submissionEntity == null)
                throw new EntityNotFoundException<Submission>();

            if (!(await HasOptionAccess(submissionEntity, ct)))
                throw new ForbiddenException();

            var submissionCommentEntity = await _context.SubmissionComments.SingleOrDefaultAsync(v => v.Id == submissionCommentId, ct);
            if (submissionCommentEntity == null)
                throw new EntityNotFoundException<SubmissionComment>();
            // delete the comment
            _context.SubmissionComments.Remove(submissionCommentEntity);
            // update and return the submission
            submissionEntity.DateModified = DateTime.UtcNow;
            submissionEntity.ModifiedBy = _user.GetId();
            await _context.SaveChangesAsync(ct);
            
            return await GetAsync(submissionId, ct);
        }

        private async Task<IEnumerable<SubmissionCategoryEntity>> CreateSubmissionCategories(
            SubmissionEntity submissionEntity, ScoringModelEntity scoringModelEntity, CancellationToken ct)
        {
            foreach (var scoringCategoryEntity in scoringModelEntity.ScoringCategories)
            {
                var submissionCategoryEntity = new SubmissionCategoryEntity();
                submissionCategoryEntity.ScoringCategoryId = scoringCategoryEntity.Id;
                submissionCategoryEntity.SubmissionId = submissionEntity.Id;
                submissionCategoryEntity.Score = 0;
                _context.SubmissionCategories.Add(submissionCategoryEntity);
                await _context.SaveChangesAsync(ct);
                await CreateSubmissionOptions(submissionCategoryEntity, scoringCategoryEntity, ct);
            }
            return submissionEntity.SubmissionCategories;
        }

        private async Task<IEnumerable<SubmissionOptionEntity>> CreateSubmissionOptions(
            SubmissionCategoryEntity submissionCategoryEntity, ScoringCategoryEntity scoringCategoryEntity, CancellationToken ct)
        {
            foreach (var scoringOptionEntity in scoringCategoryEntity.ScoringOptions)
            {
                var submissionOptionEntity = new SubmissionOptionEntity();
                submissionOptionEntity.ScoringOptionId = scoringOptionEntity.Id;
                submissionOptionEntity.SubmissionCategoryId = submissionCategoryEntity.Id;
                submissionOptionEntity.IsSelected = false;
                _context.SubmissionOptions.Add(submissionOptionEntity);
                await _context.SaveChangesAsync(ct);
            }
            return submissionCategoryEntity.SubmissionOptions;
        }

        private CategoryScore CalculateCategoryScore(SubmissionCategoryEntity submissionCategory)
        {
            var submissionOptions = submissionCategory.SubmissionOptions;
            var selections = submissionOptions.Where(so => so.IsSelected && !so.ScoringOption.IsModifier).Select(so => so.ScoringOption.Value);
            var modifiers = submissionOptions.Where(so => so.IsSelected && so.ScoringOption.IsModifier).Select(so => so.ScoringOption.Value);
            var categoryScore = new CategoryScore();
            categoryScore.MinPossibleScore = submissionOptions.Min(so => so.ScoringOption.Value);
            categoryScore.MaxPossibleScore = submissionOptions.Max(so => so.ScoringOption.Value);
            categoryScore.CategoryWeight = submissionCategory.ScoringCategory.ScoringWeight;
            // if a modifier is selected, use the max value
            // if no modifier is selected, if one is required, use 0.0, if none is required, use 1.0
            var modifier = modifiers.Any() ? modifiers.Max() : submissionCategory.ScoringCategory.IsModifierRequired ? 0.0 : 1.0;
            categoryScore.ActualScore = selections.Any() ?
                EvaluateCategoryEquation(
                    submissionCategory.ScoringCategory.CalculationEquation,
                    selections.Count(),
                    selections.Min(),
                    selections.Max(),
                    selections.Sum(),
                    modifier
                ) : 0.0;

            return categoryScore;
        }

        private double CalculateSubmissionScore(string calculationEquation, List<CategoryScore> categoryScores)
        {
            var submissionScore = categoryScores.Any() ?
                EvaluateSubmissionEquation(
                    calculationEquation,
                    categoryScores.Count(),
                    categoryScores.Sum(cs => cs.MinPossibleScore * cs.CategoryWeight),  // weighted min possible score
                    categoryScores.Sum(cs => cs.MaxPossibleScore * cs.CategoryWeight),  // weighted max possible score
                    categoryScores.Sum(cs => cs.ActualScore * cs.CategoryWeight),       // weighted actual score
                    categoryScores.Average(cs => cs.ActualScore * cs.CategoryWeight)    // weighted average score
                ) : 0.0;

            return submissionScore;
        }

        private ViewModels.Submission CreateAverageSubmission(IList<SubmissionEntity> submissionEntities)
        {
            if (submissionEntities.Count() == 0)
            {
                return null;
            }

            try
            {
                var averageSubmission = _mapper.Map<Submission>(submissionEntities.First());
                averageSubmission.ScoreIsAnAverage = true;
                // get all of the non-zero scores
                var scores = submissionEntities.Where(s => s.Score != 0.0).Select(s => s.Score);
                averageSubmission.Score = scores.Any() ? scores.Average() : 0.0;
                // get the selections for each option and the average score for each category
                if (averageSubmission.SubmissionCategories.Count > 0)
                {
                    // get all submissionCategories contained in submissionEntities
                    var submissionCategories = submissionEntities
                        .Select(s => s.SubmissionCategories)
                        .Aggregate((acc, list) => { return acc.Concat(list).ToList(); });
                    // get all submissionOptions contained in submissionEntities
                    var submissionOptions = submissionEntities
                        .Select(s => s.SubmissionCategories)
                        .Aggregate((acc, list) => { return acc.Concat(list).ToList(); })
                        .Select(c => c.SubmissionOptions)
                        .Aggregate((acc, list) => { return acc.Concat(list).ToList(); });
                    // get the average category scores
                    foreach (var category in averageSubmission.SubmissionCategories)
                    {
                        category.Score = submissionCategories
                            .Where(sc => sc.ScoringCategoryId == category.ScoringCategoryId)
                            .Average(c => c.Score);
                        // get the option selection counts
                        foreach (var option in category.SubmissionOptions)
                        {
                            option.SelectedCount = submissionOptions
                                .Where(o => o.ScoringOptionId == option.ScoringOptionId)
                                .Count(i => i.IsSelected);
                        }
                    }
                }

                return averageSubmission;
            }
            catch (System.Exception)
            {
                return null;
            }

        }

        private double EvaluateCategoryEquation(
            string calculationEquation,
            int count,
            double min,
            double max,
            double sum,
            double modifier)
        {
            var computeString = calculationEquation
                .Replace("{count}", count.ToString())
                .Replace("{min}", min.ToString())
                .Replace("{max}", max.ToString())
                .Replace("{sum}", sum.ToString())
                .Replace("{modifier}", modifier.ToString());
            double result = 0.0;
            try
            {
                result = Convert.ToDouble(new System.Data.DataTable().Compute(computeString, null));
            }
            catch (System.Exception)
            {
                result = 0.0;
            }

            return result;
        }

        private double EvaluateSubmissionEquation(
            string calculationEquation,
            int count,
            double minPossible,
            double maxPossible,
            double sum,
            double average)
        {
            // the calculation equation can include max and min values by using the format
            // MAX > math equation > MIN
            // a max can be included without a min (i.e. MAX > math equation), but not vice versa
            var maxValue = double.MaxValue;
            var minValue = double.MinValue;
            var equationString = calculationEquation;
            if (calculationEquation.Contains(">"))
            {
                var equationStrings = calculationEquation.Split(">");
                equationString = equationStrings.Count() == 1 ? equationStrings[0] : equationStrings[1];
                maxValue = double.Parse(equationStrings[0]);
                if (equationStrings.Count() > 2)
                {
                    minValue = double.Parse(equationStrings[2]);
                }
            }
            var computeString = equationString
                .Replace("{count}", count.ToString())
                .Replace("{minPossible}", minPossible.ToString())
                .Replace("{maxPossible}", maxPossible.ToString())
                .Replace("{sum}", sum.ToString())
                .Replace("{average}", average.ToString());
            double result = 0.0;
            try
            {
                result = Convert.ToDouble(new System.Data.DataTable().Compute(computeString, null));
            }
            catch (System.Exception)
            {
                result = 0.0;
            }
            // force the result to be between the min and max values
            result = Math.Max(minValue, result);
            result = Math.Min(maxValue, result);

            return result;
        }

        private async Task<bool> HasOptionAccess(SubmissionEntity submissionEntity, CancellationToken ct)
        {
            var isOnTeam = await _context.TeamUsers.AnyAsync(tu => tu.UserId == _user.GetId() && tu.TeamId == submissionEntity.TeamId, ct);
            var evaluationId = (Guid)submissionEntity.EvaluationId;
            return (
                    ((await _authorizationService.AuthorizeAsync(_user, null, new CanIncrementMoveRequirement(evaluationId, _context))).Succeeded
                        && submissionEntity.UserId == null
                        && (submissionEntity.TeamId == null || isOnTeam)) ||
                    ((await _authorizationService.AuthorizeAsync(_user, null, new CanModifyRequirement(evaluationId, _context))).Succeeded && isOnTeam) ||
                    ((await _authorizationService.AuthorizeAsync(_user, null, new CanSubmitRequirement(evaluationId, _context))).Succeeded && isOnTeam) ||
                    ((await _authorizationService.AuthorizeAsync(_user, null, new BaseUserRequirement())).Succeeded && submissionEntity.UserId == _user.GetId())
            );




            
        }


    }

    public class CategoryScore
    {
        public double MinPossibleScore { get; set; }
        public double ActualScore { get; set; }
        public double MaxPossibleScore { get; set; }
        public double CategoryWeight { get; set; }
    }

}

