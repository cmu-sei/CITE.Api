// Copyright 2026 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Security.Principal;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Cite.Api.Data;
using Cite.Api.Data.Enumerations;
using Cite.Api.Infrastructure.Authorization;
using Cite.Api.Infrastructure.Extensions;
using Cite.Api.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace Cite.Api.Services
{
    /// <summary>What a caller may see of who answered and who commented on one submission.</summary>
    public enum SubmissionNames
    {
        /// <summary>Everything, as stored.</summary>
        Shown,
        /// <summary>The submission, with no user id on it, its categories, options or comments.</summary>
        Hidden,
        /// <summary>Nothing: it is one team member's own submission.</summary>
        Withheld
    }

    /// <summary>
    /// The one place that decides whether a caller gets a team's submissions without names, for an evaluation
    /// whose <see cref="Evaluation.HideNamesFromObservers"/> is on, and that takes the names out.
    /// </summary>
    /// <remarks>
    /// The caller gets the masked view of a team when the setting is on and the caller is an outsider: not
    /// on the team, not able to manage the evaluation (its Owner role, or ManageEvaluation from any role), and
    /// not holding the system ViewEvaluations or ManageEvaluations permission. Those exceptions can already
    /// read every submission with its names (<c>evaluations/{id}/submissions</c>, <c>submissions</c>, and the
    /// hub's admin data group), so masking them would hide nothing. A Facilitator or Observer of the evaluation
    /// who is not on the team is masked. The official submission is the evaluation staff's, not a team
    /// member's, so it is never masked.
    /// </remarks>
    public interface IObserverNamesService
    {
        /// <summary>Whether the current caller gets <paramref name="teamId"/>'s submissions without names.</summary>
        Task<bool> HidesNamesAsync(Guid evaluationId, Guid teamId, CancellationToken ct);

        /// <summary>
        /// Whether <paramref name="userId"/>, with the current caller's permission claims, would get the masked
        /// view of <paramref name="teamId"/> when the evaluation hides names; the setting itself is not read.
        /// The hub sorts a connection into a team's named or unnamed group with it when the connection joins,
        /// and reads the setting when it broadcasts, so turning the setting on or off needs no rejoin.
        /// </summary>
        Task<bool> IsOutsiderAsync(Guid userId, Guid evaluationId, Guid teamId, CancellationToken ct);

        /// <summary>Whether the evaluation hides names from observers.</summary>
        Task<bool> EvaluationHidesNamesAsync(Guid evaluationId, CancellationToken ct);

        /// <summary>
        /// What the current caller may see of the names on the submission that holds the resource:
        /// a <see cref="Submission"/>, <see cref="SubmissionCategory"/>, <see cref="SubmissionOption"/> or
        /// <see cref="SubmissionComment"/>. An unknown id is <see cref="SubmissionNames.Shown"/>, so the
        /// endpoint answers it as it always has.
        /// </summary>
        Task<SubmissionNames> GetSubmissionNamesAsync<T>(Guid id, CancellationToken ct);
    }

    public class ObserverNamesService(
        CiteContext context,
        ICiteAuthorizationService authorizationService,
        IPrincipal user) : IObserverNamesService
    {
        private readonly ClaimsPrincipal _user = user as ClaimsPrincipal;

        /// <summary>Why a team member's own submission is refused to an outsider.</summary>
        public const string WithheldMessage = "This evaluation hides names from observers, so a team member's own submission is not shown to anyone outside the team.";

        public async Task<bool> HidesNamesAsync(Guid evaluationId, Guid teamId, CancellationToken ct) =>
            await EvaluationHidesNamesAsync(evaluationId, ct) &&
            await IsOutsiderAsync(_user.GetId(), evaluationId, teamId, ct);

        public async Task<bool> IsOutsiderAsync(Guid userId, Guid evaluationId, Guid teamId, CancellationToken ct)
        {
            if (await authorizationService.AuthorizeAsync<Evaluation>(evaluationId,
                    [SystemPermission.ViewEvaluations, SystemPermission.ManageEvaluations],
                    [EvaluationPermission.ManageEvaluation], ct))
                return false;

            return !await context.TeamMemberships.AnyAsync(m => m.TeamId == teamId && m.UserId == userId, ct);
        }

        public Task<bool> EvaluationHidesNamesAsync(Guid evaluationId, CancellationToken ct) =>
            EvaluationHidesNamesAsync(context, evaluationId, ct);

        /// <summary>Whether the evaluation hides names from observers, for a caller with no current user (the hub's broadcasts).</summary>
        public static Task<bool> EvaluationHidesNamesAsync(CiteContext context, Guid? evaluationId, CancellationToken ct) =>
            context.Evaluations.AnyAsync(e => e.Id == evaluationId && e.HideNamesFromObservers, ct);

        public async Task<SubmissionNames> GetSubmissionNamesAsync<T>(Guid id, CancellationToken ct)
        {
            var submissions = typeof(T) switch
            {
                var t when t == typeof(Submission) => context.Submissions.Where(s => s.Id == id),
                var t when t == typeof(SubmissionCategory) => context.SubmissionCategories.Where(c => c.Id == id).Select(c => c.Submission),
                var t when t == typeof(SubmissionOption) => context.SubmissionOptions.Where(o => o.Id == id).Select(o => o.SubmissionCategory.Submission),
                var t when t == typeof(SubmissionComment) => context.SubmissionComments.Where(c => c.Id == id).Select(c => c.SubmissionOption.SubmissionCategory.Submission),
                _ => throw new NotImplementedException($"Handler for type {typeof(T).Name} is not implemented.")
            };
            var submission = await submissions
                .Select(s => new { s.EvaluationId, s.TeamId, s.UserId })
                .FirstOrDefaultAsync(ct);
            // the official submission, and anything not found, keep their names
            if (submission?.EvaluationId == null || submission.TeamId == null)
                return SubmissionNames.Shown;
            if (!await HidesNamesAsync(submission.EvaluationId.Value, submission.TeamId.Value, ct))
                return SubmissionNames.Shown;

            return submission.UserId == null ? SubmissionNames.Hidden : SubmissionNames.Withheld;
        }

        /// <summary>
        /// A team's submission list, as an outsider sees it on an evaluation that hides names: the members' own
        /// submissions are left out, the team's submission and the averages lose every user id, and an average
        /// loses its comments, which are copied from one member's (or one team's) submission. The averages are
        /// computed from the stored submissions before this runs, so leaving members out does not change them.
        /// The official submission is unchanged.
        /// </summary>
        public static List<Submission> WithoutNames(IEnumerable<Submission> submissions) =>
            submissions
                .Where(s => s.UserId == null)
                .Select(s => s.TeamId == null && !s.ScoreIsAnAverage ? s : WithoutNames(s))
                .ToList();

        /// <summary>A copy of the submission without names, leaving <paramref name="submission"/> as it is.</summary>
        public static Submission CopyWithoutNames(Submission submission) =>
            submission == null ? null : WithoutNames(JsonSerializer.Deserialize<Submission>(JsonSerializer.Serialize(submission)));

        /// <summary>The submission with no user id on it, its categories, options or comments.</summary>
        public static Submission WithoutNames(Submission submission)
        {
            if (submission == null)
                return null;

            submission.UserId = null;
            Blank(submission);
            foreach (var category in submission.SubmissionCategories ?? [])
            {
                WithoutNames(category);
                if (submission.ScoreIsAnAverage)
                {
                    foreach (var option in category.SubmissionOptions ?? [])
                        option.SubmissionComments = new HashSet<SubmissionComment>();
                }
            }

            return submission;
        }

        public static SubmissionCategory WithoutNames(SubmissionCategory category)
        {
            if (category == null)
                return null;

            Blank(category);
            foreach (var option in category.SubmissionOptions ?? [])
                WithoutNames(option);

            return category;
        }

        public static SubmissionOption WithoutNames(SubmissionOption option)
        {
            if (option == null)
                return null;

            Blank(option);
            foreach (var comment in option.SubmissionComments ?? [])
                WithoutNames(comment);

            return option;
        }

        public static SubmissionComment WithoutNames(SubmissionComment comment)
        {
            if (comment == null)
                return null;

            Blank(comment);

            return comment;
        }

        private static void Blank(Base item)
        {
            item.CreatedBy = Guid.Empty;
            item.ModifiedBy = null;
        }
    }
}
