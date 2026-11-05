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
/// parents and owner. Navigations are left out, but for a scoring model's submissions (see
/// <see cref="ToCopy(ScoringModelEntity)"/>).
/// </summary>
[Mapper(UseDeepCloning = true)]
public static partial class EntityCopyMapper
{
    /// <remarks>
    /// The copy gets its own severity levels. Its categories are copied by the caller, one by one.
    /// Submissions the original carries are copied with it, with their categories, options and comments:
    /// only an uploaded file can carry any, since no copy loads them from the database. The copied
    /// submissions keep their references (evaluation, team, user, scoring categories and options) by id
    /// only.
    /// </remarks>
    [MapperIgnoreTarget(nameof(ScoringModelEntity.Id))]
    [MapperIgnoreTarget(nameof(ScoringModelEntity.ScoringCategories))]
    [MapperIgnoreTarget(nameof(ScoringModelEntity.Memberships))]
    public static partial ScoringModelEntity ToCopy(this ScoringModelEntity source);

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

    // A scoring model's submissions and what they own, for ToCopy(ScoringModelEntity).

    [MapperIgnoreTarget(nameof(SubmissionEntity.Id))]
    [MapperIgnoreTarget(nameof(SubmissionEntity.ScoringModel))]
    [MapperIgnoreTarget(nameof(SubmissionEntity.User))]
    [MapperIgnoreTarget(nameof(SubmissionEntity.Evaluation))]
    [MapperIgnoreTarget(nameof(SubmissionEntity.Team))]
    private static partial SubmissionEntity CopySubmission(SubmissionEntity source);

    [MapperIgnoreTarget(nameof(SubmissionCategoryEntity.Id))]
    [MapperIgnoreTarget(nameof(SubmissionCategoryEntity.Submission))]
    [MapperIgnoreTarget(nameof(SubmissionCategoryEntity.ScoringCategory))]
    private static partial SubmissionCategoryEntity CopySubmissionCategory(SubmissionCategoryEntity source);

    [MapperIgnoreTarget(nameof(SubmissionOptionEntity.Id))]
    [MapperIgnoreTarget(nameof(SubmissionOptionEntity.SubmissionCategory))]
    [MapperIgnoreTarget(nameof(SubmissionOptionEntity.ScoringOption))]
    private static partial SubmissionOptionEntity CopySubmissionOption(SubmissionOptionEntity source);

    [MapperIgnoreTarget(nameof(SubmissionCommentEntity.Id))]
    [MapperIgnoreTarget(nameof(SubmissionCommentEntity.SubmissionOption))]
    private static partial SubmissionCommentEntity CopySubmissionComment(SubmissionCommentEntity source);

    // A missing list is copied as an empty one.

    private static List<SeverityLevelEntity> CopySeverityLevels(List<SeverityLevelEntity> source) =>
        source == null ? [] : source.Select(x => CopySeverityLevel(x)).ToList();

    private static partial SeverityLevelEntity CopySeverityLevel(SeverityLevelEntity source);

    private static ICollection<SubmissionEntity> CopySubmissions(ICollection<SubmissionEntity> source) =>
        source == null ? [] : source.Select(x => CopySubmission(x)).ToList();

    private static ICollection<SubmissionCategoryEntity> CopySubmissionCategories(ICollection<SubmissionCategoryEntity> source) =>
        source == null ? [] : source.Select(x => CopySubmissionCategory(x)).ToList();

    private static ICollection<SubmissionOptionEntity> CopySubmissionOptions(ICollection<SubmissionOptionEntity> source) =>
        source == null ? [] : source.Select(x => CopySubmissionOption(x)).ToList();

    private static ICollection<SubmissionCommentEntity> CopySubmissionComments(ICollection<SubmissionCommentEntity> source) =>
        source == null ? [] : source.Select(x => CopySubmissionComment(x)).ToList();
}
