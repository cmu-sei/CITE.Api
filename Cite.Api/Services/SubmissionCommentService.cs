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
        Task<bool> LogXApiAsync(Uri verb, SubmissionOption submissionOption, String result, CancellationToken ct);
    }

    public class SubmissionCommentService : ISubmissionCommentService
    {
        private readonly CiteContext _context;
        private readonly ISubmissionService _submissionService;
        private readonly IAuthorizationService _authorizationService;
        private readonly ClaimsPrincipal _user;
        private readonly IMapper _mapper;
        private readonly IXApiService _xApiService;

        public SubmissionCommentService(
            CiteContext context,
            ISubmissionService submissionService,
            IAuthorizationService authorizationService,
            IPrincipal user,
            IXApiService xApiService,
            IMapper mapper)
        {
            _context = context;
            _submissionService = submissionService;
            _authorizationService = authorizationService;
            _user = user as ClaimsPrincipal;
            _mapper = mapper;
            _xApiService = xApiService;
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
            submissionComment.DateCreated = DateTime.UtcNow;
            submissionComment.CreatedBy = _user.GetId();
            submissionComment.DateModified = null;
            submissionComment.ModifiedBy = null;
            var submissionCommentEntity = _mapper.Map<SubmissionCommentEntity>(submissionComment);

            _context.SubmissionComments.Add(submissionCommentEntity);
            await _context.SaveChangesAsync(ct);
            submissionComment = await GetAsync(submissionCommentEntity.Id, ct);

            // create and send xapi statement
            var verb = new Uri("https://w3id.org/xapi/dod-isd/verbs/stated");
            var submissionOption = _mapper.Map<SubmissionOption>(submissionCommentEntity.SubmissionOption);
            var result = submissionComment.Comment;
            await LogXApiAsync(verb, submissionOption, result, ct);

            return submissionComment;
        }

        public async Task<SubmissionComment> UpdateAsync(Guid id, SubmissionComment submissionComment, CancellationToken ct)
        {
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
            // create and send xapi statement
            var verb = new Uri("https://w3id.org/xapi/dod-isd/verbs/edited");
            var submissionOption = _mapper.Map<SubmissionOption>(submissionCommentToUpdate.SubmissionOption);
            var result = submissionComment.Comment;
            await LogXApiAsync(verb, submissionOption, result, ct);

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
            var submissionOption = _mapper.Map<SubmissionOption>(submissionCommentToDelete.SubmissionOption);
            var result = submissionCommentToDelete.Comment;
            await LogXApiAsync(verb, submissionOption, result, ct);

            return true;
        }

        public async Task<bool> LogXApiAsync(Uri verb, SubmissionOption submissionOption, String result, CancellationToken ct)
        {

            if (_xApiService.IsConfigured())
            {
                var submissionCategory = await _context.SubmissionCategories.Where(sc => sc.Id == submissionOption.SubmissionCategoryId).FirstAsync();
                var submission = await _context.Submissions.Where(s => s.Id == submissionCategory.SubmissionId).FirstAsync();
                var evaluation = await _context.Evaluations.Where(e => e.Id == submission.EvaluationId).FirstAsync();
                var scoringCategory = await _context.ScoringCategories.Where(sc => sc.Id == submissionCategory.ScoringCategoryId).FirstAsync();
                var scoringOption = await _context.ScoringOptions.Where(so => so.Id == submissionOption.ScoringOptionId).FirstAsync();

                var teamId = (await _context.TeamMemberships
                    .SingleOrDefaultAsync(tu => tu.UserId == _user.GetId() && tu.Team.EvaluationId == submission.EvaluationId)).TeamId;

                // create and send xapi statement

                var activity = new Dictionary<String,String>();

                activity.Add("id", scoringOption.Id.ToString());
                activity.Add("name", scoringOption.Description);
                activity.Add("description", "Line item within a scoring category.");
                activity.Add("type", "scoringOption");
                activity.Add("activityType", "http://id.tincanapi.com/activitytype/resource");
                activity.Add("moreInfo", "/scoringOption/" + scoringOption.Id.ToString());
                activity.Add("result", result);

                var parent = new Dictionary<String,String>();
                parent.Add("id", evaluation.Id.ToString());
                parent.Add("name", "Evaluation");
                parent.Add("description", evaluation.Description);
                parent.Add("type", "Evaluation");
                parent.Add("activityType", "http://adlnet.gov/expapi/activities/simulation");
                parent.Add("moreInfo", "/?evaluation=" + evaluation.Id.ToString());

                var category = new Dictionary<String,String>();
                category.Add("id", scoringCategory.Id.ToString());
                category.Add("name", scoringCategory.Description);
                category.Add("description", "The scoring category type for the option.");
                category.Add("type", "scoringCategory");
                category.Add("activityType", "http://id.tincanapi.com/activitytype/category");
                category.Add("moreInfo", "");

                var grouping = new Dictionary<String,String>();
                var other = new Dictionary<String,String>();

                return await _xApiService.CreateAsync(
                    verb, activity, parent, category, grouping, other, teamId, ct);

            }
            return false;
        }
    }
}
