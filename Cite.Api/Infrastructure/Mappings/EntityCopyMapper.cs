// Copyright 2026 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using Cite.Api.Data.Models;
using Riok.Mapperly.Abstractions;

namespace Cite.Api.Infrastructure.Mappings;

/// <summary>
/// Copies of entities, for the scoring model and evaluation copies and uploads. Each copy is a new object
/// with every member of the original but the id, which is left empty; the caller gives it a new id,
/// parents and owner. The navigations each copy leaves out are listed on it.
/// </summary>
[Mapper(UseDeepCloning = true)]
public static partial class EntityCopyMapper
{
    /// <summary>
    /// A copy of the scoring model, with its own severity levels. Its categories are copied by the caller,
    /// one by one.
    /// </summary>
    /// <remarks>
    /// Submissions the original carries are copied with it, whole, as AutoMapper's same-type maps copied
    /// them: only an uploaded file can carry any, since no copy loads them from the database. See
    /// <see cref="SubmissionGraphCopy"/>.
    /// </remarks>
    public static ScoringModelEntity ToCopy(this ScoringModelEntity source) =>
        source == null ? null : new SubmissionGraphCopy().Model(source);

    [MapperIgnoreTarget(nameof(ScoringModelEntity.Id))]
    [MapperIgnoreTarget(nameof(ScoringModelEntity.ScoringCategories))]
    [MapperIgnoreTarget(nameof(ScoringModelEntity.Memberships))]
    [MapperIgnoreTarget(nameof(ScoringModelEntity.Submissions))]
    private static partial ScoringModelEntity CopyModelFields(ScoringModelEntity source);

    [MapperIgnoreTarget(nameof(ScoringCategoryEntity.Id))]
    [MapperIgnoreTarget(nameof(ScoringCategoryEntity.ScoringModel))]
    [MapperIgnoreTarget(nameof(ScoringCategoryEntity.ScoringOptions))]
    public static partial ScoringCategoryEntity ToCopy(this ScoringCategoryEntity source);

    [MapperIgnoreTarget(nameof(ScoringOptionEntity.Id))]
    [MapperIgnoreTarget(nameof(ScoringOptionEntity.ScoringCategory))]
    public static partial ScoringOptionEntity ToCopy(this ScoringOptionEntity source);

    /// <remarks>The scoring model, teams and moves are copied by the caller; submissions and memberships are not copied.</remarks>
    [MapperIgnoreTarget(nameof(EvaluationEntity.Id))]
    [MapperIgnoreTarget(nameof(EvaluationEntity.ScoringModel))]
    [MapperIgnoreTarget(nameof(EvaluationEntity.Teams))]
    [MapperIgnoreTarget(nameof(EvaluationEntity.Moves))]
    [MapperIgnoreTarget(nameof(EvaluationEntity.Submissions))]
    [MapperIgnoreTarget(nameof(EvaluationEntity.Memberships))]
    public static partial EvaluationEntity ToCopy(this EvaluationEntity source);

    [MapperIgnoreTarget(nameof(TeamEntity.Id))]
    [MapperIgnoreTarget(nameof(TeamEntity.Evaluation))]
    [MapperIgnoreTarget(nameof(TeamEntity.TeamType))]
    [MapperIgnoreTarget(nameof(TeamEntity.TeamUsers))]
    [MapperIgnoreTarget(nameof(TeamEntity.Memberships))]
    [MapperIgnoreTarget(nameof(TeamEntity.Submissions))]
    public static partial TeamEntity ToCopy(this TeamEntity source);

    [MapperIgnoreTarget(nameof(MoveEntity.Id))]
    [MapperIgnoreTarget(nameof(MoveEntity.Evaluation))]
    public static partial MoveEntity ToCopy(this MoveEntity source);

    // The members of a scoring model's submissions and what they own, without their navigations, which
    // SubmissionGraphCopy sets.

    [MapperIgnoreTarget(nameof(SubmissionEntity.Id))]
    [MapperIgnoreTarget(nameof(SubmissionEntity.ScoringModel))]
    [MapperIgnoreTarget(nameof(SubmissionEntity.User))]
    [MapperIgnoreTarget(nameof(SubmissionEntity.Evaluation))]
    [MapperIgnoreTarget(nameof(SubmissionEntity.Team))]
    [MapperIgnoreTarget(nameof(SubmissionEntity.SubmissionCategories))]
    private static partial SubmissionEntity CopySubmissionFields(SubmissionEntity source);

    [MapperIgnoreTarget(nameof(SubmissionCategoryEntity.Id))]
    [MapperIgnoreTarget(nameof(SubmissionCategoryEntity.Submission))]
    [MapperIgnoreTarget(nameof(SubmissionCategoryEntity.ScoringCategory))]
    [MapperIgnoreTarget(nameof(SubmissionCategoryEntity.SubmissionOptions))]
    private static partial SubmissionCategoryEntity CopySubmissionCategoryFields(SubmissionCategoryEntity source);

    [MapperIgnoreTarget(nameof(SubmissionOptionEntity.Id))]
    [MapperIgnoreTarget(nameof(SubmissionOptionEntity.SubmissionCategory))]
    [MapperIgnoreTarget(nameof(SubmissionOptionEntity.ScoringOption))]
    [MapperIgnoreTarget(nameof(SubmissionOptionEntity.SubmissionComments))]
    private static partial SubmissionOptionEntity CopySubmissionOptionFields(SubmissionOptionEntity source);

    [MapperIgnoreTarget(nameof(SubmissionCommentEntity.Id))]
    [MapperIgnoreTarget(nameof(SubmissionCommentEntity.SubmissionOption))]
    private static partial SubmissionCommentEntity CopySubmissionCommentFields(SubmissionCommentEntity source);

    // A missing list is copied as an empty one.
    private static List<SeverityLevelEntity> CopySeverityLevels(List<SeverityLevelEntity> source) =>
        source == null ? [] : source.Select(x => CopySeverityLevel(x)).ToList();

    private static partial SeverityLevelEntity CopySeverityLevel(SeverityLevelEntity source);

    /// <summary>
    /// One scoring model copy's submission graph, copied the way AutoMapper's same-type maps copied it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every navigation is followed. A scoring model, submission, submission category or submission option
    /// reached twice (a back-reference, or a file's <c>$ref</c>) is copied once, as AutoMapper's reference
    /// preservation did for those cyclic maps. An evaluation, team, scoring category or scoring option is
    /// copied with its own rules (above) each time it is reached, and a comment each time it is listed. A
    /// user had no same-type map, so it is the same object, and is stored as such when it is new.
    /// </para>
    /// <para>
    /// Hand-written because Mapperly's reference handling needs <c>Riok.Mapperly.Abstractions</c> at
    /// runtime, which the package reference leaves out.
    /// </para>
    /// </remarks>
    private sealed class SubmissionGraphCopy
    {
        private readonly Dictionary<object, object> _copies = new(ReferenceEqualityComparer.Instance);

        public ScoringModelEntity Model(ScoringModelEntity source)
        {
            if (source == null)
                return null;
            if (_copies.TryGetValue(source, out var done))
                return (ScoringModelEntity)done;

            var copy = CopyModelFields(source);
            _copies[source] = copy;
            copy.Submissions = (source.Submissions ?? []).Select(Submission).ToList();
            return copy;
        }

        private SubmissionEntity Submission(SubmissionEntity source)
        {
            if (source == null)
                return null;
            if (_copies.TryGetValue(source, out var done))
                return (SubmissionEntity)done;

            var copy = CopySubmissionFields(source);
            _copies[source] = copy;
            copy.ScoringModel = Model(source.ScoringModel);
            copy.User = source.User;
            copy.Evaluation = source.Evaluation.ToCopy();
            copy.Team = source.Team.ToCopy();
            copy.SubmissionCategories = (source.SubmissionCategories ?? []).Select(Category).ToList();
            return copy;
        }

        private SubmissionCategoryEntity Category(SubmissionCategoryEntity source)
        {
            if (source == null)
                return null;
            if (_copies.TryGetValue(source, out var done))
                return (SubmissionCategoryEntity)done;

            var copy = CopySubmissionCategoryFields(source);
            _copies[source] = copy;
            copy.Submission = Submission(source.Submission);
            copy.ScoringCategory = source.ScoringCategory.ToCopy();
            copy.SubmissionOptions = (source.SubmissionOptions ?? []).Select(Option).ToList();
            return copy;
        }

        private SubmissionOptionEntity Option(SubmissionOptionEntity source)
        {
            if (source == null)
                return null;
            if (_copies.TryGetValue(source, out var done))
                return (SubmissionOptionEntity)done;

            var copy = CopySubmissionOptionFields(source);
            _copies[source] = copy;
            copy.SubmissionCategory = Category(source.SubmissionCategory);
            copy.ScoringOption = source.ScoringOption.ToCopy();
            copy.SubmissionComments = (source.SubmissionComments ?? []).Select(Comment).ToList();
            return copy;
        }

        private SubmissionCommentEntity Comment(SubmissionCommentEntity source)
        {
            if (source == null)
                return null;

            var copy = CopySubmissionCommentFields(source);
            copy.SubmissionOption = Option(source.SubmissionOption);
            return copy;
        }
    }
}
