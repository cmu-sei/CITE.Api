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
using Cite.Api.Infrastructure.QueryParameters;
using Cite.Api.ViewModels;

namespace Cite.Api.Services
{
    public interface ISubmissionCommentService
    {
        Task<IEnumerable<ViewModels.SubmissionComment>> GetAsync(CancellationToken ct);
        Task<IEnumerable<ViewModels.SubmissionComment>> GetForSubmissionOptionAsync(Guid submissionOptionId, CancellationToken ct);
        Task<ViewModels.SubmissionComment> GetAsync(Guid id, CancellationToken ct);
        Task<ViewModels.SubmissionComment> CreateAsync(ViewModels.SubmissionComment submissionComment, CancellationToken ct);
        Task<ViewModels.SubmissionComment> UpdateAsync(Guid id, ViewModels.SubmissionComment submissionComment, CancellationToken ct);
        Task<bool> DeleteAsync(Guid id, CancellationToken ct);
    }

    public class SubmissionCommentService : ISubmissionCommentService
    {
        private readonly CiteContext _context;
        private readonly ISubmissionService _submissionService;
        private readonly IAuthorizationService _authorizationService;
        private readonly ClaimsPrincipal _user;
        private readonly IMapper _mapper;

        public SubmissionCommentService(
            CiteContext context,
            ISubmissionService submissionService,
            IAuthorizationService authorizationService,
            IPrincipal user,
            IMapper mapper)
        {
            _context = context;
            _submissionService = submissionService;
            _authorizationService = authorizationService;
            _user = user as ClaimsPrincipal;
            _mapper = mapper;
        }

        public async Task<IEnumerable<ViewModels.SubmissionComment>> GetAsync(CancellationToken ct)
        {
            if (!(await _authorizationService.AuthorizeAsync(_user, null, new ContentDeveloperRequirement())).Succeeded)
                throw new ForbiddenException();

            var submissionComments = _context.SubmissionComments;

            return _mapper.Map<IEnumerable<SubmissionComment>>(await submissionComments.ToListAsync());
        }

        public async Task<IEnumerable<ViewModels.SubmissionComment>> GetForSubmissionOptionAsync(Guid submissionOptionId, CancellationToken ct)
        {
            if (!(await HasAccess(submissionOptionId, ct)))
                throw new ForbiddenException();

            var submissionComments = _context.SubmissionComments.Where(sc => sc.SubmissionOptionId == submissionOptionId);

            return _mapper.Map<IEnumerable<SubmissionComment>>(await submissionComments.ToListAsync());
        }

        public async Task<ViewModels.SubmissionComment> GetAsync(Guid id, CancellationToken ct)
        {
            var item = await _context.SubmissionComments.SingleOrDefaultAsync(sc => sc.Id == id, ct);

            if (!(await HasAccess(item.SubmissionOptionId, ct)))
                throw new ForbiddenException();

            return _mapper.Map<SubmissionComment>(item);
        }

        public async Task<ViewModels.SubmissionComment> CreateAsync(ViewModels.SubmissionComment submissionComment, CancellationToken ct)
        {
            if (!(await HasAccess(submissionComment.SubmissionOptionId, ct)))
                throw new ForbiddenException();

            submissionComment.Id = submissionComment.Id != Guid.Empty ? submissionComment.Id : Guid.NewGuid();
            submissionComment.DateCreated = DateTime.UtcNow;
            submissionComment.CreatedBy = _user.GetId();
            submissionComment.DateModified = null;
            submissionComment.ModifiedBy = null;
            var submissionCommentEntity = _mapper.Map<SubmissionCommentEntity>(submissionComment);

            _context.SubmissionComments.Add(submissionCommentEntity);
            await _context.SaveChangesAsync(ct);
            submissionComment = await GetAsync(submissionCommentEntity.Id, ct);

            return submissionComment;
        }

        public async Task<ViewModels.SubmissionComment> UpdateAsync(Guid id, ViewModels.SubmissionComment submissionComment, CancellationToken ct)
        {
            if (!(await HasAccess(submissionComment.SubmissionOptionId, ct)))
                throw new ForbiddenException();

            var submissionCommentToUpdate = await _context.SubmissionComments.SingleOrDefaultAsync(v => v.Id == id, ct);

            if (submissionCommentToUpdate == null)
                throw new EntityNotFoundException<SubmissionComment>();

            submissionComment.CreatedBy = submissionCommentToUpdate.CreatedBy;
            submissionComment.DateCreated = submissionCommentToUpdate.DateCreated;
            submissionComment.ModifiedBy = _user.GetId();
            submissionComment.DateModified = DateTime.UtcNow;
            _mapper.Map(submissionComment, submissionCommentToUpdate);

            _context.SubmissionComments.Update(submissionCommentToUpdate);
            await _context.SaveChangesAsync(ct);

            submissionComment = await GetAsync(submissionCommentToUpdate.Id, ct);

            return submissionComment;
        }

        public async Task<bool> DeleteAsync(Guid id, CancellationToken ct)
        {
            var submissionCommentToDelete = await _context.SubmissionComments.SingleOrDefaultAsync(v => v.Id == id, ct);

            if (submissionCommentToDelete == null)
                throw new EntityNotFoundException<SubmissionComment>();

            if (!(await HasAccess(submissionCommentToDelete.SubmissionOptionId, ct)))
                throw new ForbiddenException();

            _context.SubmissionComments.Remove(submissionCommentToDelete);
            await _context.SaveChangesAsync(ct);

            return true;
        }

        private async Task<bool> HasAccess(Guid submissionOptionId, CancellationToken ct)
        {
            var submissionOption = await _context.SubmissionOptions.FindAsync(submissionOptionId);
            var submissionCategoryEntity = await _context.SubmissionCategories.FindAsync(submissionOption.SubmissionCategoryId);
            var submissionEntity = await _context.Submissions.FindAsync(submissionCategoryEntity.SubmissionId);
            var isOnTeam = await _context.TeamUsers.AnyAsync(tu => tu.UserId == _user.GetId() && tu.TeamId == submissionEntity.TeamId, ct);
            var evaluationId = (Guid)submissionEntity.EvaluationId;
            return (
                    ((await _authorizationService.AuthorizeAsync(_user, null, new CanIncrementMoveRequirement(evaluationId))).Succeeded
                        && submissionEntity.UserId == null
                        && (submissionEntity.TeamId == null || isOnTeam)) ||
                    ((await _authorizationService.AuthorizeAsync(_user, null, new CanModifyRequirement(evaluationId))).Succeeded && isOnTeam) ||
                    ((await _authorizationService.AuthorizeAsync(_user, null, new CanSubmitRequirement(evaluationId))).Succeeded && isOnTeam) ||
                    ((await _authorizationService.AuthorizeAsync(_user, null, new BaseUserRequirement())).Succeeded && submissionEntity.UserId == _user.GetId())
            );
        }

    }
}

