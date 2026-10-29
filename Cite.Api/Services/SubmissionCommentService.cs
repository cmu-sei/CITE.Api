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
        Task<IEnumerable<SubmissionComment>> GetForSubmissionOptionAsync(Guid submissionOptionId, CancellationToken ct);
        Task<SubmissionComment> GetAsync(Guid id, CancellationToken ct);
        Task<SubmissionComment> CreateAsync(SubmissionComment submissionComment, CancellationToken ct);
        Task<SubmissionComment> UpdateAsync(Guid id, UpdateSubmissionCommentRequest submissionComment, CancellationToken ct);
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

        public async Task<IEnumerable<SubmissionComment>> GetForSubmissionOptionAsync(Guid submissionOptionId, CancellationToken ct)
        {
            var submissionComments = _context.SubmissionComments.Where(sc => sc.SubmissionOptionId == submissionOptionId);

            return _mapper.Map<IEnumerable<SubmissionComment>>(await submissionComments.ToListAsync());
        }

        public async Task<SubmissionComment> GetAsync(Guid id, CancellationToken ct)
        {
            var item = await _context.SubmissionComments.SingleOrDefaultAsync(sc => sc.Id == id, ct);

            return _mapper.Map<SubmissionComment>(item);
        }

        // Writes go through the same implementation as the nested /submissions/{id}/comments routes,
        // scoped to the submission that owns the option or comment.

        public async Task<SubmissionComment> CreateAsync(SubmissionComment submissionComment, CancellationToken ct)
        {
            var submissionId = await _context.SubmissionOptions
                .Where(o => o.Id == submissionComment.SubmissionOptionId)
                .Select(o => (Guid?)o.SubmissionCategory.SubmissionId)
                .SingleOrDefaultAsync(ct)
                ?? throw new EntityNotFoundException<SubmissionOption>();
            var submissionCommentEntity = await _submissionService.AddCommentEntityAsync(submissionId, submissionComment.SubmissionOptionId, submissionComment.Comment, ct);

            return await GetAsync(submissionCommentEntity.Id, ct);
        }

        public async Task<SubmissionComment> UpdateAsync(Guid id, UpdateSubmissionCommentRequest submissionComment, CancellationToken ct)
        {
            var submissionId = await GetSubmissionIdAsync(id, ct);
            await _submissionService.UpdateCommentEntityAsync(submissionId, id, submissionComment.Comment, ct);

            return await GetAsync(id, ct);
        }

        public async Task<bool> DeleteAsync(Guid id, CancellationToken ct)
        {
            var submissionId = await GetSubmissionIdAsync(id, ct);
            await _submissionService.DeleteCommentEntityAsync(submissionId, id, ct);

            return true;
        }

        private async Task<Guid> GetSubmissionIdAsync(Guid submissionCommentId, CancellationToken ct)
        {
            return await _context.SubmissionComments
                .Where(c => c.Id == submissionCommentId)
                .Select(c => (Guid?)c.SubmissionOption.SubmissionCategory.SubmissionId)
                .SingleOrDefaultAsync(ct)
                ?? throw new EntityNotFoundException<SubmissionComment>();
        }
    }
}
