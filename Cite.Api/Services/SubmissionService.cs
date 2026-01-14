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
using Cite.Api.Infrastructure.Options;
using Cite.Api.Infrastructure.QueryParameters;
using Cite.Api.ViewModels;

namespace Cite.Api.Services
{
    public interface ISubmissionService
    {
        Task<IEnumerable<ViewModels.Submission>> GetAsync(SubmissionGet queryParameters, CancellationToken ct);
        Task<IEnumerable<ViewModels.Submission>> GetByEvaluationAsync(Guid evaluationId, CancellationToken ct);
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
        Task<ViewModels.Submission> GetTeamAverageAsync(SubmissionEntity submission, CancellationToken ct);
        Task<ViewModels.Submission> GetTypeAverageAsync(Submission submission, CancellationToken ct);
        Task<ViewModels.Submission> AddCommentAsync(Guid submissionId, SubmissionComment submissionComment, CancellationToken ct);
        Task<ViewModels.Submission> UpdateCommentAsync(Guid submissionId, Guid submissionCommentId, SubmissionComment submissionComment, CancellationToken ct);
        Task<ViewModels.Submission> DeleteCommentAsync(Guid submissionId, Guid submissionCommentId, CancellationToken ct);
        Task<bool> LogXApiAsync(Uri verb, Submission submission, SubmissionOption submissionOption, CancellationToken ct);
        Task CreateMoveSubmissions(MoveEntity moveEntity, CiteContext citeContext, CancellationToken ct);
        Task CreateTeamSubmissions(TeamEntity teamEntity, CiteContext citeContext, CancellationToken ct);
        Task CreateUserSubmissions(TeamMembershipEntity teamMembershipEntity, CiteContext citeContext, CancellationToken ct);
        Task<bool> HasSpecificPermission<T>(Guid submissionId, SpecificPermission specificPermission, CancellationToken ct);
    }

    public class SubmissionService : ISubmissionService
    {
        private readonly CiteContext _context;
        private readonly ICiteAuthorizationService _authorizationService;
        private readonly ClaimsPrincipal _user;
        private readonly IMapper _mapper;
        private readonly DatabaseOptions _options;
        private readonly ILogger<SubmissionService> _logger;
        private readonly IMoveService _moveService;
        private readonly IXApiService _xApiService;

        public SubmissionService(
            CiteContext context,
            ICiteAuthorizationService authorizationService,
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
            var userId = Guid.Empty;
            var hasUserId = false;
            var evaluationId = Guid.Empty;
            var hasEvaluationId = false;
            var scoringModelId = Guid.Empty;
            var hasScoringModelId = false;
            var teamId = Guid.Empty;
            var hasTeamId = false;
            // check queryParameters for filter terms
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

        public async Task<IEnumerable<ViewModels.Submission>> GetByEvaluationAsync(Guid evaluationId, CancellationToken ct)
        {
            var currentMoveNumber = (await _context.Evaluations.FindAsync(evaluationId)).CurrentMoveNumber;
            var scoringModel = (await _context.Evaluations.Include(e => e.ScoringModel).SingleOrDefaultAsync(e => e.Id == evaluationId)).ScoringModel;
            var submissionEntities = _context.Submissions
                .Include(sm => sm.SubmissionCategories)
                .ThenInclude(sc => sc.SubmissionOptions)
                .ThenInclude(so => so.SubmissionComments)
                .Where(sm => sm.EvaluationId == evaluationId && sm.MoveNumber <= currentMoveNumber);
            var submissionEntityList = await submissionEntities
                .ToListAsync();
            var submissions = _mapper.Map<IEnumerable<Submission>>(submissionEntityList).ToList();

            return submissions;
        }

        public async Task<IEnumerable<ViewModels.Submission>> GetMineByEvaluationAsync(Guid evaluationId, CancellationToken ct)
        {
            var userId = _user.GetId();
            var team = await _context.TeamMemberships
                .Where(tu => tu.UserId == userId && tu.Team.EvaluationId == evaluationId)
                .Include(tu => tu.Team.TeamType)
                .Select(tu => tu.Team).FirstAsync();
            var teamId = team.Id;
            var isContributor = team.TeamType.IsOfficialScoreContributor;
            var currentMoveNumber = (await _context.Evaluations.FindAsync(evaluationId)).CurrentMoveNumber;
            var scoringModel = (await _context.Evaluations.Include(e => e.ScoringModel).SingleOrDefaultAsync(e => e.Id == evaluationId)).ScoringModel;
            var submissionEntities = _context.Submissions
                .Include(sm => sm.SubmissionCategories)
                .ThenInclude(sc => sc.SubmissionOptions)
                .ThenInclude(so => so.SubmissionComments)
                .Where(sm =>
                    (sm.UserId == userId && sm.TeamId == teamId && sm.EvaluationId == evaluationId) ||
                    (sm.UserId == null && sm.TeamId == teamId && sm.EvaluationId == evaluationId) ||
                    (sm.UserId == null && sm.TeamId == null && sm.EvaluationId == evaluationId && sm.MoveNumber < currentMoveNumber) ||
                    (sm.UserId == null && sm.TeamId == null && sm.EvaluationId == evaluationId && sm.MoveNumber == currentMoveNumber && isContributor)
                );
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
                .ToListAsync();
            var submissions = _mapper.Map<IEnumerable<Submission>>(submissionEntityList).ToList();
            var averageSubmissions = await GetTeamAndTypeAveragesAsync(evaluationId, team, currentMoveNumber, scoringModel.UseTeamAverageScore, scoringModel.UseTypeAverageScore, ct);
            if (averageSubmissions.Any())
            {
                submissions.AddRange(averageSubmissions);
            }

            return submissions;
        }

        public async Task<IEnumerable<ViewModels.Submission>> GetByEvaluationTeamAsync(Guid evaluationId, Guid teamId, CancellationToken ct)
        {
            var userId = _user.GetId();
            var team = await _context.Teams
                .Include(t => t.TeamType)
                .SingleOrDefaultAsync(t => t.Id == teamId);
            var isContributor = team.TeamType.IsOfficialScoreContributor;
            var currentMoveNumber = (await _context.Evaluations.FindAsync(evaluationId)).CurrentMoveNumber;
            var scoringModel = await _context.ScoringModels.Where(m => m.EvaluationId == evaluationId).AsNoTracking().FirstOrDefaultAsync(ct);
            var submissionEntities = await _context.Submissions.Where(sm =>
                (sm.TeamId == teamId && sm.EvaluationId == evaluationId) ||
                (sm.UserId == null && sm.TeamId == null && sm.EvaluationId == evaluationId && sm.MoveNumber < currentMoveNumber && scoringModel.UseOfficialScore) ||
                (sm.UserId == null && sm.TeamId == null && sm.EvaluationId == evaluationId && sm.MoveNumber == currentMoveNumber && isContributor && scoringModel.UseOfficialScore))
                .Include(sm => sm.SubmissionCategories)
                .ThenInclude(sc => sc.SubmissionOptions)
                .ThenInclude(so => so.SubmissionComments)
                .ToListAsync();
            var submissions = _mapper.Map<IEnumerable<Submission>>(submissionEntities).ToList();
            var averageSubmissions = await GetTeamAndTypeAveragesAsync(evaluationId, team, currentMoveNumber, scoringModel.UseTeamAverageScore, team.TeamType.ShowTeamTypeAverage, ct);
            if (averageSubmissions.Any())
            {
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
            for (var move = 0; move <= currentMoveNumber; move++)
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
            for (var move = 0; move <= currentMoveNumber; move++)
            {
                var moveSubmissions = submissionEntities.Where(s => s.MoveNumber == move).ToList();
            }
            if (team.TeamType != null && team.TeamType.IsOfficialScoreContributor)
            {
                // calculate the average of teams in the team type
                var teamIds = await _context.Teams.Where(t => t.TeamTypeId == team.TeamTypeId).Select(t => t.Id).ToListAsync(ct);
                submissionEntities = await _context.Submissions.Where(sm =>
                    (sm.UserId == null && teamIds.Contains((Guid)sm.TeamId) && sm.EvaluationId == evaluationId)).ToListAsync(ct);
                for (var move = 0; move <= currentMoveNumber; move++)
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

        public async Task<ViewModels.Submission> GetTeamAverageAsync(SubmissionEntity submission, CancellationToken ct)
        {
            if (submission.TeamId == null)
            {
                return null;
            }
            var move = submission.MoveNumber;
            // calculate the average of users on the team
            var teamMembershipSubmissions = await _context.Submissions
                .Where(sm => (sm.UserId != null && sm.TeamId == submission.TeamId && sm.EvaluationId == submission.EvaluationId && sm.MoveNumber == move))
                .Include(s => s.SubmissionCategories)
                .ThenInclude(sc => sc.SubmissionOptions)
                .ToListAsync(ct);
            var teamAverageSubmission = CreateAverageSubmission(teamMembershipSubmissions);
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
            var item = await _context.Submissions
                .Include(sm => sm.SubmissionCategories)
                .ThenInclude(sc => sc.SubmissionOptions)
                .ThenInclude(so => so.SubmissionComments)
                .SingleOrDefaultAsync(sm => sm.Id == id, ct);
            if (item == null)
                throw new EntityNotFoundException<Submission>("Submission not found " + id.ToString());

            return _mapper.Map<Submission>(item);
        }

        public async Task<ViewModels.Submission> CreateAsync(ViewModels.Submission submission, CancellationToken ct)
        {
            var requestedSubmissionEntity = await CreateNewSubmission(_context, submission, ct);
            // create and send xapi statement
            var verb = new Uri("https://w3id.org/xapi/dod-isd/verbs/initiated");
            await LogXApiAsync(verb, submission, null, ct);

            return await GetAsync(requestedSubmissionEntity.Id, ct);
        }

        public async Task<ViewModels.Submission> UpdateAsync(Guid id, ViewModels.Submission submission, CancellationToken ct)
        {
            var submissionToUpdate = await _context.Submissions.SingleOrDefaultAsync(v => v.Id == id, ct);
            if (submissionToUpdate == null)
                throw new EntityNotFoundException<Submission>();

            submission.ModifiedBy = _user.GetId();
            _mapper.Map(submission, submissionToUpdate);

            _context.Submissions.Update(submissionToUpdate);
            await _context.SaveChangesAsync(ct);

            submission = await GetAsync(submissionToUpdate.Id, ct);

            // create and send xapi statement
            var verb = new Uri("https://w3id.org/xapi/dod-isd/verbs/edited");
            if (submission.Status == Data.Enumerations.ItemStatus.Complete)
            {
                verb = new Uri("https://w3id.org/xapi/dod-isd/verbs/submitted");
            }
            await LogXApiAsync(verb, submission, null, ct);

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
            var isOnTeam = await _context.TeamMemberships.AnyAsync(tu => tu.UserId == _user.GetId() && tu.TeamId == submissionEntity.TeamId);
            var evaluationId = (Guid)submissionEntity.EvaluationId;

            var modifiedBy = _user.GetId();
            // Only one Modifier can be selected
            if (submissionOptionToUpdate.ScoringOption.IsModifier && value)
            {
                var submissionOptionsToClear = _context.SubmissionOptions.Include(so => so.ScoringOption).Where(so =>
                    so.SubmissionCategoryId == submissionCategoryEntity.Id && so.ScoringOption.IsModifier && so.IsSelected);
                foreach (var submissionOption in submissionOptionsToClear)
                {
                    submissionOption.IsSelected = false;
                    submissionOption.ModifiedBy = modifiedBy;
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
                        so.SubmissionCategoryId == submissionCategoryEntity.Id && so.IsSelected);
                    foreach (var submissionOption in submissionOptionsToClear)
                    {
                        submissionOption.IsSelected = false;
                        submissionOption.ModifiedBy = modifiedBy;
                    }
                }
            }
            // update submission option
            submissionOptionToUpdate.IsSelected = value;
            submissionOptionToUpdate.ModifiedBy = modifiedBy;
            // update submission
            submissionEntity.ModifiedBy = modifiedBy;
            await _context.SaveChangesAsync(ct);
            submissionEntity = await UpdateScoreAsync(ct, submissionEntity.Id);

            var verb = new Uri("https://w3id.org/xapi/dod-isd/verbs/selected");
            if (value == false)
            {
                verb = new Uri("https://w3id.org/xapi/dod-isd/verbs/reset");
            }
            await LogXApiAsync(verb, null, _mapper.Map<SubmissionOption>(submissionOptionToUpdate), ct);

            return await GetAsync(submissionEntity.Id, ct);
        }

        public async Task<SubmissionEntity> CreateNewSubmission(CiteContext citeContext, ViewModels.Submission submission, CancellationToken ct)
        {
            // An evaluationId must be supplied
            if (submission.EvaluationId == Guid.Empty)
                throw new ArgumentException("An Evaluation ID must be supplied to create a new submission");
            // actually create a new submission
            var submissionEntity = _mapper.Map<SubmissionEntity>(submission);
            submissionEntity.Id = Guid.NewGuid();
            submissionEntity.CreatedBy = _user.GetId();
            submissionEntity.Status = Data.Enumerations.ItemStatus.Active;
            submissionEntity.Evaluation = null;
            submissionEntity.ScoringModel = null;
            var scoringModelEntity = await citeContext.ScoringModels
                .Include(sm => sm.ScoringCategories)
                .ThenInclude(sc => sc.ScoringOptions)
                .FirstAsync(sm => sm.Id == submissionEntity.ScoringModelId);
            CreateSubmissionCategories(submissionEntity, scoringModelEntity, ct);
            citeContext.Submissions.Add(submissionEntity);
            await citeContext.SaveChangesAsync(ct);

            return submissionEntity;
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
            var verb = new Uri("https://w3id.org/xapi/dod-isd/verbs/evaluated"); // could be interacted
            await LogXApiAsync(verb, _mapper.Map<Submission>(submissionEntity), null, ct);

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

            var isOnTeam = await _context.TeamMemberships.AnyAsync(tu => tu.UserId == _user.GetId() && tu.TeamId == submissionToClear.TeamId);
            var evaluationId = (Guid)submissionToClear.EvaluationId;
            if (submissionToClear.Status != ItemStatus.Active)
                throw new Exception($"Cannot clear selections of a submission ({submissionToClear.Id}) that is not currently active.");

            foreach (var submissionCategory in submissionToClear.SubmissionCategories)
            {
                foreach (var submissionOption in submissionCategory.SubmissionOptions)
                {
                    if (submissionOption.IsSelected)
                    {
                        submissionOption.IsSelected = false;
                        submissionOption.ModifiedBy = _user.GetId();
                    }
                    foreach (var submissionComment in submissionOption.SubmissionComments)
                    {
                        _context.SubmissionComments.Remove(submissionComment);
                    }
                }
                submissionCategory.Score = 0.0;
            }
            ;
            submissionToClear.Score = 0.0;
            _context.Submissions.Update(submissionToClear);
            await _context.SaveChangesAsync(ct);
            var submission = await GetAsync(id, ct);

            // create and send xapi statement
            var verb = new Uri("https://w3id.org/xapi/dod-isd/verbs/reset"); // could be interacted or initialized
            await LogXApiAsync(verb, submission, null, ct);

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

            var isOnTeam = await _context.TeamMemberships.AnyAsync(tu => tu.UserId == _user.GetId() && tu.TeamId == targetSubmission.TeamId);
            var evaluationId = (Guid)targetSubmission.EvaluationId;
            if (targetSubmission.Status != ItemStatus.Active)
                throw new Exception($"Cannot preset selections of a submission ({targetSubmission.Id}) that is not currently active.");

            var baseSubmission = await _context.Submissions
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
                        if (submissionOption.IsSelected != baseSubmissionOption.IsSelected)
                        {
                            submissionOption.IsSelected = baseSubmissionOption.IsSelected;
                            submissionOption.ModifiedBy = _user.GetId();
                        }
                    }
                    targetSubmissionCategory.Score = baseSubmissionCategory.Score;
                }
                ;
                _context.Submissions.Update(targetSubmission);
                await _context.SaveChangesAsync(ct);
            }
            var submission = await GetAsync(id, ct);

            // create and send xapi statement
            var verb = new Uri("https://w3id.org/xapi/dod-isd/verbs/initialized"); // could be interacted
            await LogXApiAsync(verb, submission, null, ct);

            return submission;
        }

        public async Task<bool> DeleteAsync(Guid id, CancellationToken ct)
        {
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

            // Add the submission comment
            submissionComment.Id = submissionComment.Id != Guid.Empty ? submissionComment.Id : Guid.NewGuid();
            submissionComment.CreatedBy = _user.GetId();
            var submissionCommentEntity = _mapper.Map<SubmissionCommentEntity>(submissionComment);
            _context.SubmissionComments.Add(submissionCommentEntity);
            // update and return the submission
            submissionEntity.ModifiedBy = submissionComment.CreatedBy;
            await _context.SaveChangesAsync(ct);

            return await GetAsync(submissionId, ct);
        }

        public async Task<ViewModels.Submission> UpdateCommentAsync(Guid submissionId, Guid submissionCommentId, SubmissionComment submissionComment, CancellationToken ct)
        {
            var submissionEntity = await _context.Submissions.SingleOrDefaultAsync(v => v.Id == submissionId, ct);
            if (submissionEntity == null)
                throw new EntityNotFoundException<Submission>();

            var submissionCommentToUpdate = await _context.SubmissionComments.SingleOrDefaultAsync(v => v.Id == submissionCommentId, ct);
            if (submissionCommentToUpdate == null)
                throw new EntityNotFoundException<SubmissionComment>();

            submissionComment.ModifiedBy = _user.GetId();
            _mapper.Map(submissionComment, submissionCommentToUpdate);
            _context.SubmissionComments.Update(submissionCommentToUpdate);
            // update and return the submission
            submissionEntity.ModifiedBy = submissionComment.CreatedBy;
            await _context.SaveChangesAsync(ct);

            return await GetAsync(submissionId, ct);
        }

        public async Task<ViewModels.Submission> DeleteCommentAsync(Guid submissionId, Guid submissionCommentId, CancellationToken ct)
        {
            var submissionEntity = await _context.Submissions.SingleOrDefaultAsync(v => v.Id == submissionId, ct);
            if (submissionEntity == null)
                throw new EntityNotFoundException<Submission>();

            var submissionCommentEntity = await _context.SubmissionComments.SingleOrDefaultAsync(v => v.Id == submissionCommentId, ct);
            if (submissionCommentEntity == null)
                throw new EntityNotFoundException<SubmissionComment>();
            // delete the comment
            _context.SubmissionComments.Remove(submissionCommentEntity);
            // update and return the submission
            submissionEntity.ModifiedBy = _user.GetId();
            await _context.SaveChangesAsync(ct);

            return await GetAsync(submissionId, ct);
        }

        public async Task<bool> HasSpecificPermission<T>(Guid id, SpecificPermission specificPermission, CancellationToken ct)
        {
            var submissionId = typeof(T) switch
            {
                var t when t == typeof(Submission) => id,
                var t when t == typeof(SubmissionCategory) => await _context.SubmissionCategories.Where(m => m.Id == id).Select(m => m.SubmissionId).FirstOrDefaultAsync(),
                var t when t == typeof(SubmissionOption) => await _context.SubmissionOptions.Where(m => m.Id == id).Select(m => m.SubmissionCategory.SubmissionId).FirstOrDefaultAsync(),
                var t when t == typeof(SubmissionComment) => await _context.SubmissionComments.Where(m => m.Id == id).Select(m => m.SubmissionOption.SubmissionCategory.SubmissionId).FirstOrDefaultAsync(),
                _ => throw new NotImplementedException($"Handler for type {typeof(T).Name} is not implemented.")
            };
            var submission = await _context.Submissions.FirstOrDefaultAsync(m => m.Id == submissionId, ct);
            return await HasSpecificPermission(submission, specificPermission, ct);
        }

        public async Task<bool> HasSpecificPermission(SubmissionEntity submission, SpecificPermission specificPermission, CancellationToken ct)
        {
            var hasAccess = false;
            var userId = _user.GetId();
            var isUserSubmission = submission.UserId != null;
            var isTeamSubmission = !isUserSubmission && submission.TeamId != null;
            var isOfficialSubmission = !isUserSubmission && !isTeamSubmission;
            if (isUserSubmission)
            {
                // User can do everything with their submission
                hasAccess = submission.UserId == userId;
            }
            else
            {
                var teamMembership = await _context.TeamMemberships.FirstOrDefaultAsync(m => m.UserId == userId && m.Team.EvaluationId == submission.EvaluationId);
                // must be a team member to do anything else
                if (teamMembership != null)
                {
                    if (isOfficialSubmission)
                    {
                        if (specificPermission == SpecificPermission.View)
                        {
                            var currentMoveNumber = (await _context.Evaluations.FindAsync(submission.EvaluationId)).CurrentMoveNumber;
                            if (submission.MoveNumber < currentMoveNumber)
                            {
                                // has access to current official score
                                hasAccess = await _authorizationService.AuthorizeAsync<Team>(teamMembership.TeamId, [], [TeamPermission.ViewCurrentOfficialScore], ct);
                            }
                            else
                            {
                                // has access to previous official score
                                hasAccess = await _authorizationService.AuthorizeAsync<Team>(teamMembership.TeamId, [], [TeamPermission.ViewPastOfficialScore], ct);
                            }
                        }
                        else
                        {
                            hasAccess = await _authorizationService.AuthorizeAsync<Team>(teamMembership.TeamId, [], [TeamPermission.EditOfficialScore], ct);
                        }
                    }
                    else
                    {
                        switch (specificPermission)
                        {
                            case SpecificPermission.View:
                                hasAccess = await _authorizationService.AuthorizeAsync<Team>(teamMembership.TeamId, [], [TeamPermission.ViewTeam], ct);
                                break;
                            case SpecificPermission.Score:
                                hasAccess = await _authorizationService.AuthorizeAsync<Team>(teamMembership.TeamId, [], [TeamPermission.EditTeamScore], ct);
                                break;
                            case SpecificPermission.Submit:
                                hasAccess = await _authorizationService.AuthorizeAsync<Team>(teamMembership.TeamId, [], [TeamPermission.SubmitTeamScore], ct);
                                break;
                        }
                    }
                }
            }

            return hasAccess;
        }

        private IEnumerable<SubmissionCategoryEntity> CreateSubmissionCategories(
            SubmissionEntity submissionEntity, ScoringModelEntity scoringModelEntity, CancellationToken ct)
        {
            foreach (var scoringCategoryEntity in scoringModelEntity.ScoringCategories)
            {
                var submissionCategoryEntity = new SubmissionCategoryEntity()
                {
                    Id = Guid.NewGuid(),
                    ScoringCategoryId = scoringCategoryEntity.Id,
                    SubmissionId = submissionEntity.Id,
                    Score = 0,
                };
                CreateSubmissionOptions(submissionCategoryEntity, scoringCategoryEntity, ct);
                submissionEntity.SubmissionCategories.Add(submissionCategoryEntity);
            }
            return submissionEntity.SubmissionCategories;
        }

        private IEnumerable<SubmissionOptionEntity> CreateSubmissionOptions(
            SubmissionCategoryEntity submissionCategoryEntity, ScoringCategoryEntity scoringCategoryEntity, CancellationToken ct)
        {
            foreach (var scoringOptionEntity in scoringCategoryEntity.ScoringOptions)
            {
                var submissionOptionEntity = new SubmissionOptionEntity()
                {
                    Id = Guid.NewGuid(),
                    ScoringOptionId = scoringOptionEntity.Id,
                    SubmissionCategoryId = submissionCategoryEntity.Id,
                    IsSelected = false
                };
                submissionCategoryEntity.SubmissionOptions.Add(submissionOptionEntity);
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
        public async Task<bool> LogXApiAsync(Uri verb, Submission submission, SubmissionOption submissionOption, CancellationToken ct)
        {

            if (_xApiService.IsConfigured())
            {
                ScoringOptionEntity scoringOption = null;
                SubmissionCategoryEntity submissionCategory = null;
                ScoringCategoryEntity scoringCategory = null;

                if (submissionOption != null)
                {
                    scoringOption = await _context.ScoringOptions.Where(so => so.Id == submissionOption.ScoringOptionId).FirstAsync();
                    submissionCategory = await _context.SubmissionCategories.Where(sc => sc.Id == submissionOption.SubmissionCategoryId).FirstAsync();
                    scoringCategory = await _context.ScoringCategories.Where(sc => sc.Id == submissionCategory.ScoringCategoryId).FirstAsync();
                }
                if ((submission == null) && (submissionCategory != null))
                {
                    // TODO make this async
                    submission = _mapper.Map<Submission>(_context.Submissions.Where(s => s.Id == submissionCategory.SubmissionId).First());
                }
                var move = _mapper.Map<Move>(_context.Moves.Where(m => m.MoveNumber == submission.MoveNumber).First());

                var teamId = (await _context.TeamMemberships
                    .SingleOrDefaultAsync(tu => tu.UserId == _user.GetId() && tu.Team.EvaluationId == submission.EvaluationId)).TeamId;

                var evaluation = await _context.Evaluations.Where(e => e.Id == submission.EvaluationId).FirstAsync();

                // create and send xapi statement

                var activity = new Dictionary<String, String>();
                if (scoringOption != null)
                {
                    activity.Add("id", scoringOption.Id.ToString());
                    activity.Add("name", scoringOption.Description);
                    activity.Add("description", "Line item within a scoring category.");
                    activity.Add("type", "scoringOption");
                    activity.Add("activityType", "http://id.tincanapi.com/activitytype/resource");
                    activity.Add("moreInfo", "/scoringOption/" + scoringOption.Id.ToString());
                }
                else
                {
                    // log the submission
                    activity.Add("id", submission.Id.ToString());
                    activity.Add("name", "New Submission");
                    activity.Add("description", "A score submitted to assess an incident.");
                    activity.Add("type", "submission");
                    activity.Add("activityType", "http://id.tincanapi.com/activitytype/resource");
                    activity.Add("moreInfo", "/submission/" + submission.Id.ToString());
                }

                var parent = new Dictionary<String, String>();
                parent.Add("id", evaluation.Id.ToString());
                parent.Add("name", "Evaluation");
                parent.Add("description", evaluation.Description);
                parent.Add("type", "Evaluation");
                parent.Add("activityType", "http://adlnet.gov/expapi/activities/simulation");
                parent.Add("moreInfo", "/?evaluation=" + evaluation.Id.ToString());

                var category = new Dictionary<String, String>();
                if (scoringCategory != null)
                {
                    category.Add("id", scoringCategory.Id.ToString());
                    category.Add("name", scoringCategory.Description);
                    category.Add("description", "The scoring category type for the option.");
                    category.Add("type", "scoringCategory");
                    category.Add("activityType", "http://id.tincanapi.com/activitytype/category");
                    category.Add("moreInfo", "");
                }
                // TODO maybe add all scoring categories
                var grouping = new Dictionary<String, String>();
                grouping.Add("id", move.Id.ToString());
                grouping.Add("name", move.Description);
                grouping.Add("description", "The exercise move associated with the score.");
                grouping.Add("type", "move");
                grouping.Add("activityType", "http://id.tincanapi.com/activitytype/collection-simple");
                grouping.Add("moreInfo", "");

                var other = new Dictionary<String, String>();

                // TODO determine if we should log exhibit as registration
                return await _xApiService.CreateAsync(
                    verb, activity, parent, category, grouping, other, teamId, ct);

            }
            return false;
        }

        public async Task CreateMoveSubmissions(MoveEntity move, CiteContext citeContext, CancellationToken ct)
        {
            var evaluation = await citeContext.Evaluations.AsNoTracking().FirstOrDefaultAsync(m => m.Id == move.EvaluationId);
            var scoringModel = await citeContext.ScoringModels.AsNoTracking().FirstOrDefaultAsync(m => m.Id == evaluation.ScoringModelId, ct);
            var teams = await citeContext.Teams.AsNoTracking().Where(m => m.EvaluationId == evaluation.Id).ToListAsync(ct);
            var teamMemberships = await citeContext.TeamMemberships.AsNoTracking().Where(m => m.Team.EvaluationId == evaluation.Id).ToListAsync(ct);
            var officialSubmission = new Submission()
            {
                Id = Guid.NewGuid(),
                EvaluationId = evaluation.Id,
                TeamId = null,
                UserId = null,
                ScoringModelId = scoringModel.Id,
                MoveNumber = move.MoveNumber,
                Status = Data.Enumerations.ItemStatus.Active
            };
            await CreateNewSubmission(citeContext, officialSubmission, ct);
            foreach (var team in teams)
            {
                var teamSubmission = new Submission()
                {
                    Id = Guid.NewGuid(),
                    EvaluationId = (Guid)team.EvaluationId,
                    TeamId = team.Id,
                    UserId = null,
                    ScoringModelId = scoringModel.Id,
                    MoveNumber = move.MoveNumber,
                    Status = Data.Enumerations.ItemStatus.Active
                };
                await CreateNewSubmission(citeContext, teamSubmission, ct);
            }
            foreach (var teamMembership in teamMemberships)
            {
                var userSubmission = new Submission()
                {
                    Id = Guid.NewGuid(),
                    EvaluationId = evaluation.Id,
                    TeamId = teamMembership.TeamId,
                    UserId = teamMembership.UserId,
                    ScoringModelId = scoringModel.Id,
                    MoveNumber = move.MoveNumber,
                    Status = Data.Enumerations.ItemStatus.Active
                };
                await CreateNewSubmission(citeContext, userSubmission, ct);
            }
        }

        public async Task CreateTeamSubmissions(TeamEntity team, CiteContext citeContext, CancellationToken ct)
        {
            var evaluation = await citeContext.Evaluations.AsNoTracking().Include(m => m.Moves).FirstOrDefaultAsync(m => m.Id == team.EvaluationId, ct);
            var scoringModel = await citeContext.ScoringModels.AsNoTracking().FirstOrDefaultAsync(m => m.Id == evaluation.ScoringModelId, ct);
            foreach (var move in evaluation.Moves)
            {
                var submission = new Submission()
                {
                    Id = Guid.NewGuid(),
                    EvaluationId = move.EvaluationId,
                    TeamId = team.Id,
                    UserId = null,
                    ScoringModelId = scoringModel.Id,
                    MoveNumber = move.MoveNumber,
                    Status = Data.Enumerations.ItemStatus.Active
                };
                await CreateNewSubmission(citeContext, submission, ct);
            }
        }

        public async Task CreateUserSubmissions(TeamMembershipEntity teamMembership, CiteContext citeContext, CancellationToken ct)
        {
            var evaluation = await citeContext.Teams.AsNoTracking().Where(m => m.Id == teamMembership.TeamId).Select(m => m.Evaluation).FirstOrDefaultAsync(ct);
            var scoringModel = await citeContext.ScoringModels.AsNoTracking().FirstOrDefaultAsync(m => m.Id == evaluation.ScoringModelId, ct);
            var moves = await citeContext.Moves.AsNoTracking().Where(m => m.EvaluationId == evaluation.Id).ToListAsync(ct);
            foreach (var move in moves)
            {
                var submission = new Submission()
                {
                    Id = Guid.NewGuid(),
                    EvaluationId = move.EvaluationId,
                    TeamId = teamMembership.TeamId,
                    UserId = teamMembership.UserId,
                    ScoringModelId = scoringModel.Id,
                    MoveNumber = move.MoveNumber,
                    Status = Data.Enumerations.ItemStatus.Active
                };
                await CreateNewSubmission(citeContext, submission, ct);
            }
        }

    }

    public class CategoryScore
    {
        public double MinPossibleScore { get; set; }
        public double ActualScore { get; set; }
        public double MaxPossibleScore { get; set; }
        public double CategoryWeight { get; set; }
    }

    public enum SpecificPermission
    {
        View,
        Score,
        Submit
    }

}
