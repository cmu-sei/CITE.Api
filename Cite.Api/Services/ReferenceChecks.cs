// Copyright 2026 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Cite.Api.Data;
using Cite.Api.Infrastructure.Exceptions;

namespace Cite.Api.Services
{
    /// <summary>
    /// Checks that a record a create names in its body belongs to the parent the caller is authorized on.
    /// A reference outside that parent is a 409. They run for every caller, including system permission holders.
    /// </summary>
    public static class ReferenceChecks
    {
        public static async Task EnsureTeamInEvaluationAsync(this CiteContext context, Guid? teamId, Guid? evaluationId, CancellationToken ct)
        {
            if (teamId != null && !await context.Teams.AnyAsync(t => t.Id == teamId && t.EvaluationId == evaluationId, ct))
                throw new ConflictException("The team is not part of the evaluation.");
        }

        public static async Task EnsureUserOnTeamAsync(this CiteContext context, Guid? userId, Guid? teamId, CancellationToken ct)
        {
            if (userId != null && !await context.TeamMemberships.AnyAsync(m => m.UserId == userId && m.TeamId == teamId, ct))
                throw new ConflictException("The user is not a member of the team.");
        }

        public static async Task EnsureScoringModelOfEvaluationAsync(this CiteContext context, Guid scoringModelId, Guid? evaluationId, CancellationToken ct)
        {
            if (!await context.Evaluations.AnyAsync(e => e.Id == evaluationId && e.ScoringModelId == scoringModelId, ct))
                throw new ConflictException("The scoring model is not the evaluation's scoring model.");
        }

        public static async Task EnsureScoringCategoryOfSubmissionAsync(this CiteContext context, Guid scoringCategoryId, Guid submissionId, CancellationToken ct)
        {
            if (!await context.Submissions.AnyAsync(s => s.Id == submissionId &&
                context.ScoringCategories.Any(c => c.Id == scoringCategoryId && c.ScoringModelId == s.ScoringModelId), ct))
                throw new ConflictException("The scoring category is not part of the submission's scoring model.");
        }

        public static async Task EnsureScoringOptionOfSubmissionCategoryAsync(this CiteContext context, Guid scoringOptionId, Guid submissionCategoryId, CancellationToken ct)
        {
            if (!await context.SubmissionCategories.AnyAsync(c => c.Id == submissionCategoryId &&
                context.ScoringOptions.Any(o => o.Id == scoringOptionId && o.ScoringCategoryId == c.ScoringCategoryId), ct))
                throw new ConflictException("The scoring option is not part of the submission category's scoring category.");
        }
    }
}
