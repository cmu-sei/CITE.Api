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
        Task<SubmissionComment> UpdateAsync(Guid id, SubmissionComment submissionComment, CancellationToken ct);
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

        public async Task<SubmissionComment> CreateAsync(SubmissionComment submissionComment, CancellationToken ct)
        {
            submissionComment.Id = submissionComment.Id != Guid.Empty ? submissionComment.Id : Guid.NewGuid();
            submissionComment.CreatedBy = _user.GetId();
            var submissionCommentEntity = _mapper.Map<SubmissionCommentEntity>(submissionComment);

            _context.SubmissionComments.Add(submissionCommentEntity);
            await _context.SaveChangesAsync(ct);
            submissionComment = await GetAsync(submissionCommentEntity.Id, ct);

            // create and send xapi statement
            var verb = new Uri("https://w3id.org/xapi/dod-isd/verbs/stated");
            await _submissionService.LogCommentXApiAsync(
                verb,
                submissionCommentEntity.SubmissionOptionId,
                submissionComment.Comment,
                ct);

            return submissionComment;
        }

        public async Task<SubmissionComment> UpdateAsync(Guid id, SubmissionComment submissionComment, CancellationToken ct)
        {
            var submissionCommentToUpdate = await _context.SubmissionComments.SingleOrDefaultAsync(v => v.Id == id, ct);
            if (submissionCommentToUpdate == null)
                throw new EntityNotFoundException<SubmissionComment>();

            submissionComment.ModifiedBy = _user.GetId();
            _mapper.Map(submissionComment, submissionCommentToUpdate);
            _context.SubmissionComments.Update(submissionCommentToUpdate);
            await _context.SaveChangesAsync(ct);
            submissionComment = await GetAsync(submissionCommentToUpdate.Id, ct);
            // create and send xapi statement
            var verb = new Uri("https://w3id.org/xapi/dod-isd/verbs/edited");
            await _submissionService.LogCommentXApiAsync(
                verb,
                submissionCommentToUpdate.SubmissionOptionId,
                submissionComment.Comment,
                ct);

            return submissionComment;
        }

        public async Task<bool> DeleteAsync(Guid id, CancellationToken ct)
        {
            var submissionCommentToDelete = await _context.SubmissionComments.SingleOrDefaultAsync(v => v.Id == id, ct);
            if (submissionCommentToDelete == null)
                throw new EntityNotFoundException<SubmissionComment>();

            _context.SubmissionComments.Remove(submissionCommentToDelete);
            await _context.SaveChangesAsync(ct);
            // create and send xapi statement
            var verb = new Uri("https://w3id.org/xapi/dod-isd/verbs/deleted");
            await _submissionService.LogCommentXApiAsync(
                verb,
                submissionCommentToDelete.SubmissionOptionId,
                submissionCommentToDelete.Comment,
                ct);

            return true;
        }
    }
}
