// Copyright 2022 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license, please see LICENSE.md in the project root for license information or contact permission@sei.cmu.edu for full terms.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
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
        Task<IEnumerable<ViewModels.Evaluation>> GetUserEvaluationsAsync(Guid userId, CancellationToken ct);
        Task<ViewModels.Evaluation> GetAsync(Guid id, CancellationToken ct);
        Task<ViewModels.Evaluation> CreateAsync(ViewModels.Evaluation evaluation, CancellationToken ct);
        Task<ViewModels.Evaluation> CopyAsync(Guid evaluationId, CancellationToken ct);
        Task<Tuple<MemoryStream, string>> DownloadJsonAsync(Guid evaluationId, CancellationToken ct);
        Task<Evaluation> UploadJsonAsync(FileForm form, CancellationToken ct);
        Task<ViewModels.Evaluation> UpdateAsync(Guid id, ViewModels.Evaluation evaluation, CancellationToken ct);
        Task<ViewModels.Evaluation> UpdateSituationAsync(Guid id, EvaluationSituation evaluationSituation, CancellationToken ct);
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
        private readonly IMoveService _moveService;
        private readonly IScoringModelService _scoringModelService;
        private readonly ITeamTypeService _teamTypeService;

        public EvaluationService(
            CiteContext context,
            IAuthorizationService authorizationService,
            IPrincipal user,
            IMapper mapper,
            ISubmissionService submissionService,
            IMoveService moveService,
            IScoringModelService scoringModelService,
            ITeamTypeService teamTypeService,
            ILogger<EvaluationService> logger)
        {
            _context = context;
            _authorizationService = authorizationService;
            _user = user as ClaimsPrincipal;
            _mapper = mapper;
            _submissionService = submissionService;
            _moveService = moveService;
            _scoringModelService = scoringModelService;
            _teamTypeService = teamTypeService;
            _logger = logger;
        }

        public async Task<IEnumerable<ViewModels.Evaluation>> GetAsync(EvaluationGet queryParameters, CancellationToken ct)
        {
            // only content developers can get all of the evaluations
            if (!(await _authorizationService.AuthorizeAsync(_user, null, new ContentDeveloperRequirement())).Succeeded)
                throw new ForbiddenException();

            IQueryable<EvaluationEntity> evaluations = null;

            // filter based on user
            if (!String.IsNullOrEmpty(queryParameters.UserId))
            {
                Guid userId;
                Guid.TryParse(queryParameters.UserId, out userId);
                evaluations = _context.Evaluations
                    .Include(e => e.Moves)
                    .Where(sm => sm.CreatedBy == userId);
            }
            // filter based on Scoring Model
            if (!String.IsNullOrEmpty(queryParameters.ScoringModelId))
            {
                Guid scoringModelId;
                Guid.TryParse(queryParameters.ScoringModelId, out scoringModelId);
                if (evaluations == null)
                {
                    evaluations = _context.Evaluations
                        .Include(e => e.Moves)
                        .Where(sm => sm.CreatedBy == scoringModelId);
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
                    evaluations = _context.Evaluations
                        .Include(e => e.Moves)
                        .Where(sm => sm.Description.Contains(queryParameters.Description));
                }
                else
                {
                    evaluations = evaluations.Where(sm => sm.Description.Contains(queryParameters.Description));
                }
            }
            else if (evaluations == null)
            {
                evaluations = _context.Evaluations.Include(e => e.Moves);
            }

            return _mapper.Map<IEnumerable<Evaluation>>(await evaluations.ToListAsync());
        }

        public async Task<IEnumerable<ViewModels.Evaluation>> GetMineAsync(CancellationToken ct)
        {
            var userId = _user.GetId();

            return await GetUserEvaluationsAsync(userId, ct);
        }

        public async Task<IEnumerable<ViewModels.Evaluation>> GetUserEvaluationsAsync(Guid userId, CancellationToken ct)
        {
            var currentUserId = _user.GetId();
            if (currentUserId == userId)
            {
                if (!(await _authorizationService.AuthorizeAsync(_user, null, new BaseUserRequirement())).Succeeded)
                    throw new ForbiddenException();
            }
            else
            {
                if (!(await _authorizationService.AuthorizeAsync(_user, null, new FullRightsRequirement())).Succeeded)
                    throw new ForbiddenException();
            }

            var evaluationIdList =  await _context.TeamUsers
                .Where(tu => tu.UserId == userId)
                .Select(tu => tu.Team.EvaluationId)
                .ToListAsync(ct);
            var evaluationList = await _context.Evaluations
                .Where(e => evaluationIdList.Contains(e.Id) && e.Status != ItemStatus.Cancelled && e.Status != ItemStatus.Archived)
                .ToListAsync(ct);

            return _mapper.Map<IEnumerable<Evaluation>>(evaluationList);
        }

        public async Task<ViewModels.Evaluation> GetAsync(Guid id, CancellationToken ct)
        {
            if (!(await _authorizationService.AuthorizeAsync(_user, null, new BaseUserRequirement())).Succeeded)
                throw new ForbiddenException();

            var item = await _context.Evaluations
                .Include(e => e.Teams)
                .Include(e => e.Moves)
                .SingleOrDefaultAsync(sm => sm.Id == id, ct);

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
            evaluationEntity.SituationTime = evaluationEntity.SituationTime.ToUniversalTime();
            _context.Evaluations.Add(evaluationEntity);
            await _context.SaveChangesAsync(ct);
            evaluation = await GetAsync(evaluationEntity.Id, ct);
            // create a default move, if necessary
            if (evaluation.Moves.Count() == 0) {
              ViewModels.Move move = new Move();
              move.Description = "Default Move";
              move.MoveNumber = 0;
              move.SituationTime = evaluation.SituationTime;
              move.EvaluationId = evaluation.Id;
              await _moveService.CreateAsync(move, ct);
            }
            // create the official and team submissions, if necessary
            await VerifyOfficialAndTeamSubmissions(evaluationEntity, ct);

            return await GetAsync(evaluation.Id, ct);
        }

        public async Task<ViewModels.Evaluation> CopyAsync(Guid evaluationId, CancellationToken ct)
        {
            if (!(await _authorizationService.AuthorizeAsync(_user, null, new ContentDeveloperRequirement())).Succeeded)
                throw new ForbiddenException();

            var evaluationEntity = await _context.Evaluations
                .AsNoTracking()
                .Include(m => m.Teams)
                .Include(m => m.Moves)
                .AsSplitQuery()
                .SingleOrDefaultAsync(m => m.Id == evaluationId);
            if (evaluationEntity == null)
                throw new EntityNotFoundException<EvaluationEntity>("Evaluation not found with ID=" + evaluationId.ToString());

            var newEvaluationEntity = await privateEvaluationCopyAsync(evaluationEntity, ct);
            var evaluation = _mapper.Map<Evaluation>(newEvaluationEntity);

            return evaluation;
        }

        private async Task<EvaluationEntity> privateEvaluationCopyAsync(EvaluationEntity evaluationEntity, CancellationToken ct)
        {
            var currentUserId = _user.GetId();
            var username = (await _context.Users.SingleOrDefaultAsync(u => u.Id == _user.GetId())).Name;
            evaluationEntity.Id = Guid.NewGuid();
            evaluationEntity.DateCreated = DateTime.UtcNow;
            evaluationEntity.CreatedBy = currentUserId;
            evaluationEntity.DateModified = evaluationEntity.DateCreated;
            evaluationEntity.ModifiedBy = evaluationEntity.CreatedBy;
            evaluationEntity.Description = evaluationEntity.Description + " - " + username;
            // copy teams
            foreach (var team in evaluationEntity.Teams)
            {
                team.Id = Guid.NewGuid();
                team.EvaluationId = evaluationEntity.Id;
                team.Evaluation = null;
                team.DateCreated = evaluationEntity.DateCreated;
                team.CreatedBy = evaluationEntity.CreatedBy;
            }
            // copy moves
            foreach (var move in evaluationEntity.Moves)
            {
                move.Id = Guid.NewGuid();
                move.EvaluationId = evaluationEntity.Id;
                move.Evaluation = null;
                move.DateCreated = evaluationEntity.DateCreated;
                move.CreatedBy = evaluationEntity.CreatedBy;
            }
            _context.Evaluations.Add(evaluationEntity);
            await _context.SaveChangesAsync(ct);

            // get the new Evaluation to return
            evaluationEntity = await _context.Evaluations
                .Include(m => m.Teams)
                .ThenInclude(t => t.TeamType)
                .Include(m => m.Moves)
                .AsSplitQuery()
                .SingleOrDefaultAsync(sm => sm.Id == evaluationEntity.Id, ct);

            return evaluationEntity;
        }

        public async Task<Tuple<MemoryStream, string>> DownloadJsonAsync(Guid evaluationId, CancellationToken ct)
        {
            // user must be a Content Developer
            if (!(await _authorizationService.AuthorizeAsync(_user, null, new ContentDeveloperRequirement())).Succeeded)
                throw new ForbiddenException();

            var evaluation = await _context.Evaluations
                .Include(m => m.ScoringModel)
                .ThenInclude(m => m.ScoringCategories)
                .ThenInclude(sc => sc.ScoringOptions)
                .Include(m => m.Teams)
                .ThenInclude(t => t.TeamType)
                .Include(m => m.Moves)
                .AsSplitQuery()
                .SingleOrDefaultAsync(sm => sm.Id == evaluationId, ct);
            if (evaluation == null)
            {
                throw new EntityNotFoundException<EvaluationEntity>("Evaluation not found " + evaluationId);
            }

            var evaluationJson = "";
            var options = new JsonSerializerOptions()
            {
                ReferenceHandler = ReferenceHandler.Preserve
            };
            evaluationJson = JsonSerializer.Serialize(evaluation, options);
            // convert string to stream
            byte[] byteArray = Encoding.ASCII.GetBytes(evaluationJson);
            MemoryStream memoryStream = new MemoryStream(byteArray);
            var filename = evaluation.Description.ToLower().EndsWith(".json") ? evaluation.Description : evaluation.Description + ".json";

            return System.Tuple.Create(memoryStream, filename);
        }

        public async Task<Evaluation> UploadJsonAsync(FileForm form, CancellationToken ct)
        {
            // user must be a Content Developer
            if (!(await _authorizationService.AuthorizeAsync(_user, null, new ContentDeveloperRequirement())).Succeeded)
                throw new ForbiddenException();

            var uploadItem = form.ToUpload;
            var evaluationJson = "";
            using (StreamReader reader = new StreamReader(uploadItem.OpenReadStream()))
            {
                // convert stream to string
                evaluationJson = reader.ReadToEnd();
            }
            var options = new JsonSerializerOptions()
            {
                ReferenceHandler = ReferenceHandler.Preserve
            };
            var evaluationEntity = JsonSerializer.Deserialize<EvaluationEntity>(evaluationJson, options);
            // if the scoring model doesn't exist, create it
            var exists = await _context.ScoringModels.AnyAsync(m => m.Id == evaluationEntity.ScoringModelId, ct);
            if (!exists)
            {
                var newScoringModel = await _scoringModelService.InternalScoringModelEntityCopyAsync(evaluationEntity.ScoringModel, ct);
                evaluationEntity.ScoringModelId = newScoringModel.Id;
            }
            evaluationEntity.ScoringModel = null;
            // if TeamTypes don't exist, then create them
            foreach (var team in evaluationEntity.Teams)
            {
                exists = await _context.TeamTypes.AnyAsync(m => m.Id == team.TeamTypeId, ct);
                if (!exists)
                {
                    await _teamTypeService.InternalCreateAsync(_mapper.Map<TeamType>(team.TeamType), ct);
                }
                team.TeamType = null;
            }
            // make a copy and add it to the database
            evaluationEntity = await privateEvaluationCopyAsync(evaluationEntity, ct);

            return _mapper.Map<Evaluation>(evaluationEntity);
        }

        public async Task<ViewModels.Evaluation> UpdateAsync(Guid id, ViewModels.Evaluation evaluation, CancellationToken ct)
        {
            if (!(await _authorizationService.AuthorizeAsync(_user, null, new ContentDeveloperRequirement())).Succeeded &&
                !(await _authorizationService.AuthorizeAsync(_user, null, new CanIncrementMoveRequirement(id, _context))).Succeeded)
                throw new ForbiddenException();

            var evaluationToUpdate = await _context.Evaluations.SingleOrDefaultAsync(v => v.Id == id, ct);

            if (evaluationToUpdate == null)
                throw new EntityNotFoundException<Evaluation>();

            // make sure no evaluation user is on more than one team if setting this evaluation to Active
            if (evaluation.Status == ItemStatus.Active && evaluationToUpdate.Status != ItemStatus.Active)
            {
                // get the teams for this evaluation
                var evaluationTeamList = await _context.Teams
                    .Include(t => t.TeamUsers)
                    .ThenInclude(tu => tu.User)
                    .Where(t => t.EvaluationId == evaluation.Id)
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

        public async Task<ViewModels.Evaluation> UpdateSituationAsync(Guid id, EvaluationSituation evaluationSituation, CancellationToken ct)
        {
            if (!(await _authorizationService.AuthorizeAsync(_user, null, new ContentDeveloperRequirement())).Succeeded &&
                !(await _authorizationService.AuthorizeAsync(_user, null, new CanIncrementMoveRequirement(id, _context))).Succeeded)
                throw new ForbiddenException();

            var evaluationToUpdate = await _context.Evaluations.SingleOrDefaultAsync(v => v.Id == id, ct);

            if (evaluationToUpdate == null)
                throw new EntityNotFoundException<Evaluation>();

            evaluationToUpdate.SituationTime = evaluationSituation.SituationTime;
            evaluationToUpdate.SituationDescription = evaluationSituation.SituationDescription;
            _context.Evaluations.Update(evaluationToUpdate);
            await _context.SaveChangesAsync(ct);

            var evaluation = await GetAsync(evaluationToUpdate.Id, ct);

            return evaluation;
        }

        public async Task<ViewModels.Evaluation> SetCurrentMoveAsync(Guid id, int moveNumber, CancellationToken ct)
        {
            if (!(await _authorizationService.AuthorizeAsync(_user, null, new ContentDeveloperRequirement())).Succeeded &&
                !(await _authorizationService.AuthorizeAsync(_user, null, new CanIncrementMoveRequirement(id, _context))).Succeeded)
                throw new ForbiddenException();

            var evaluationToUpdate = await _context.Evaluations
                .Include(e => e.Moves)
                .SingleOrDefaultAsync(v => v.Id == id, ct);
            if (evaluationToUpdate == null)
                throw new EntityNotFoundException<Evaluation>();
            var move = evaluationToUpdate.Moves.SingleOrDefault(m => m.MoveNumber == moveNumber);
            if (move == null)
                throw new EntityNotFoundException<Move>();

            evaluationToUpdate.ModifiedBy = _user.GetId();
            evaluationToUpdate.DateModified = DateTime.UtcNow;
            evaluationToUpdate.CurrentMoveNumber = moveNumber;
            evaluationToUpdate.SituationDescription = move.SituationDescription;
            evaluationToUpdate.SituationTime = move.SituationTime;
            await VerifyOfficialAndTeamSubmissions(evaluationToUpdate, ct);

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
            var evaluationTeamList = await _context.Teams
                .Include(t => t.TeamUsers)
                .ThenInclude(tu => tu.User)
                .Where(t => t.EvaluationId == evaluation.Id)
                .AsNoTracking()
                .ToListAsync();
            // get a list of moves for the evaluation
            var moves = await _moveService.GetByEvaluationAsync(evaluation.Id, ct);
            // verify submissions exist for all moves
            foreach (var move in moves)
            {
                // make sure all official and team submissions exist
                if (!submissionList.Any(s => s.UserId == null && s.TeamId == null && s.MoveNumber == move.MoveNumber))
                {
                    var submission = new Submission() {
                        Id = Guid.NewGuid(),
                        EvaluationId = evaluation.Id,
                        TeamId = null,
                        UserId = null,
                        ScoringModelId = evaluation.ScoringModelId,
                        MoveNumber = move.MoveNumber,
                        DateCreated = DateTime.UtcNow
                    };
                    _logger.LogInformation("Make Official submission for move " + move.MoveNumber.ToString());
                    await _submissionService.CreateNewSubmission(_context, submission, ct);
                }
                // team submissions
                foreach (var team in evaluationTeamList)
                {
                    if (!submissionList.Any(s => s.UserId == null && s.TeamId == team.Id && s.MoveNumber == move.MoveNumber))
                    {
                        var submission = new Submission() {
                            Id = Guid.NewGuid(),
                            EvaluationId = evaluation.Id,
                            TeamId = team.Id,
                            UserId = null,
                            ScoringModelId = evaluation.ScoringModelId,
                            MoveNumber = move.MoveNumber,
                        DateCreated = DateTime.UtcNow
                        };
                        _logger.LogInformation("Make Team submission for move " + move.MoveNumber + "  team=" + submission.TeamId.ToString());
                        await _submissionService.CreateNewSubmission(_context, submission, ct);
                    }
                }
            }

        }

    }


    public class EvaluationSituation
    {
        public DateTime SituationTime { get; set; }
        public string SituationDescription { get; set; }
    }
}
