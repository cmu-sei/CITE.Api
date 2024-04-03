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
using Cite.Api.Infrastructure.QueryParameters;
using Cite.Api.ViewModels;

namespace Cite.Api.Services
{
    public interface ISubmissionOptionService
    {
        Task<IEnumerable<ViewModels.SubmissionOption>> GetAsync(SubmissionOptionGet queryParameters, CancellationToken ct);
        Task<IEnumerable<ViewModels.SubmissionOption>> GetForSubmissionCategoryAsync(Guid submissionCategoryId, CancellationToken ct);
        Task<ViewModels.SubmissionOption> GetAsync(Guid id, CancellationToken ct);
        Task<ViewModels.SubmissionOption> CreateAsync(ViewModels.SubmissionOption submissionOption, CancellationToken ct);
        Task<ViewModels.SubmissionOption> UpdateAsync(Guid id, ViewModels.SubmissionOption submissionOption, CancellationToken ct);
        Task<bool> DeleteAsync(Guid id, CancellationToken ct);
    }

    public class SubmissionOptionService : ISubmissionOptionService
    {
        private readonly CiteContext _context;
        private readonly ISubmissionService _submissionService;
        private readonly IAuthorizationService _authorizationService;
        private readonly ClaimsPrincipal _user;
        private readonly IMapper _mapper;
        private readonly DatabaseOptions _options;

        public SubmissionOptionService(
            CiteContext context,
            ISubmissionService submissionService,
            IAuthorizationService authorizationService,
            IPrincipal user,
            IMapper mapper,
            DatabaseOptions options)
        {
            _context = context;
            _submissionService = submissionService;
            _authorizationService = authorizationService;
            _user = user as ClaimsPrincipal;
            _mapper = mapper;
            _options = options;
        }

        public async Task<IEnumerable<ViewModels.SubmissionOption>> GetAsync(SubmissionOptionGet queryParameters, CancellationToken ct)
        {
            if (!(await _authorizationService.AuthorizeAsync(_user, null, new BaseUserRequirement())).Succeeded)
                throw new ForbiddenException();

            var submissionOptions = _context.SubmissionOptions;

            return _mapper.Map<IEnumerable<SubmissionOption>>(await submissionOptions.ToListAsync());
        }

        public async Task<IEnumerable<ViewModels.SubmissionOption>> GetForSubmissionCategoryAsync(Guid submissionCategoryId, CancellationToken ct)
        {
            if (!(await _authorizationService.AuthorizeAsync(_user, null, new BaseUserRequirement())).Succeeded)
                throw new ForbiddenException();

            var submissionOptions = _context.SubmissionOptions.Where(sc => sc.SubmissionCategoryId == submissionCategoryId).Include(so => so.SubmissionComments);

            return _mapper.Map<IEnumerable<SubmissionOption>>(await submissionOptions.ToListAsync());
        }

        public async Task<ViewModels.SubmissionOption> GetAsync(Guid id, CancellationToken ct)
        {
            if (!(await _authorizationService.AuthorizeAsync(_user, null, new BaseUserRequirement())).Succeeded)
                throw new ForbiddenException();

            var item = await _context.SubmissionOptions
                .Include(so => so.SubmissionComments)
                .SingleOrDefaultAsync(sc => sc.Id == id, ct);

            return _mapper.Map<SubmissionOption>(item);
        }

        public async Task<ViewModels.SubmissionOption> CreateAsync(ViewModels.SubmissionOption submissionOption, CancellationToken ct)
        {
            if (!(await _authorizationService.AuthorizeAsync(_user, null, new ContentDeveloperRequirement())).Succeeded)
                throw new ForbiddenException();

            submissionOption.Id = submissionOption.Id != Guid.Empty ? submissionOption.Id : Guid.NewGuid();
            submissionOption.DateCreated = DateTime.UtcNow;
            submissionOption.CreatedBy = _user.GetId();
            submissionOption.DateModified = null;
            submissionOption.ModifiedBy = null;
            var submissionOptionEntity = _mapper.Map<SubmissionOptionEntity>(submissionOption);

            _context.SubmissionOptions.Add(submissionOptionEntity);
            await _context.SaveChangesAsync(ct);
            submissionOption = await GetAsync(submissionOptionEntity.Id, ct);

            return submissionOption;
        }

        public async Task<ViewModels.SubmissionOption> UpdateAsync(Guid id, ViewModels.SubmissionOption submissionOption, CancellationToken ct)
        {
            var submissionOptionToUpdate = await _context.SubmissionOptions.SingleOrDefaultAsync(v => v.Id == id, ct);
            if (submissionOptionToUpdate == null)
                throw new EntityNotFoundException<SubmissionOption>();

            var item = (await _context.SubmissionCategories.FirstAsync(s => s.Id == submissionOption.SubmissionCategoryId)).Submission;
            var userId = _user.GetId();
            var team = await _context.TeamUsers
                .Where(tu => tu.UserId == userId)
                .Include(tu => tu.Team.TeamType)
                .Select(tu => tu.Team).FirstAsync();
            var teamId = team.Id;
            var isCollaborator = team.TeamType.IsOfficialScoreContributor;
            var currentMoveNumber = (await _context.Evaluations.FindAsync(item.EvaluationId)).CurrentMoveNumber;
            var isIncrementer = (await _authorizationService.AuthorizeAsync(_user, null, new CanIncrementMoveRequirement((Guid)team.EvaluationId, _context))).Succeeded;
            var hasAccess =
                (item.UserId == userId && item.TeamId == teamId && item.EvaluationId == item.EvaluationId) ||
                (item.UserId == null && item.TeamId == teamId && item.EvaluationId == item.EvaluationId) ||
                (item.UserId == null && item.TeamId == null && item.EvaluationId == item.EvaluationId && item.MoveNumber < currentMoveNumber) ||
                (item.UserId == null && item.TeamId == null && item.EvaluationId == item.EvaluationId && item.MoveNumber == currentMoveNumber && (isCollaborator || isIncrementer));
            if (!hasAccess)
                throw new ForbiddenException();

            submissionOption.CreatedBy = submissionOptionToUpdate.CreatedBy;
            submissionOption.DateCreated = submissionOptionToUpdate.DateCreated;
            submissionOption.ModifiedBy = _user.GetId();
            submissionOption.DateModified = DateTime.UtcNow;
            _mapper.Map(submissionOption, submissionOptionToUpdate);

            _context.SubmissionOptions.Update(submissionOptionToUpdate);
            await _context.SaveChangesAsync(ct);

            submissionOption = await GetAsync(submissionOptionToUpdate.Id, ct);

            return submissionOption;
        }

        public async Task<bool> DeleteAsync(Guid id, CancellationToken ct)
        {
            if (!(await _authorizationService.AuthorizeAsync(_user, null, new ContentDeveloperRequirement())).Succeeded)
                throw new ForbiddenException();

            var submissionOptionToDelete = await _context.SubmissionOptions.SingleOrDefaultAsync(v => v.Id == id, ct);

            if (submissionOptionToDelete == null)
                throw new EntityNotFoundException<SubmissionOption>();

            _context.SubmissionOptions.Remove(submissionOptionToDelete);
            await _context.SaveChangesAsync(ct);

            return true;
        }

    }
}

