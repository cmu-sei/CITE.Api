// Copyright 2026 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using Cite.Api.Data.Models;
using Cite.Api.ViewModels;
using Riok.Mapperly.Abstractions;

namespace Cite.Api.Infrastructure.Mappings;

/// <summary>
/// Submissions' mappings. <see cref="Submission.GroupId"/> and <see cref="Submission.ScoreIsAnAverage"/>
/// describe an average submission, which <c>SubmissionService</c> builds; they are never read from or
/// written to the entity. A missing category list is an empty one in both directions.
/// </summary>
[Mapper]
[UseStaticMapper(typeof(SubmissionCategoryMapper))]
public static partial class SubmissionMapper
{
    /// <remarks>A submission without an evaluation is read with an empty <see cref="Submission.EvaluationId"/>.</remarks>
    [MapperIgnoreTarget(nameof(Submission.GroupId))]
    [MapperIgnoreTarget(nameof(Submission.ScoreIsAnAverage))]
    public static partial Submission ToSubmission(this SubmissionEntity source);

    /// <remarks>
    /// The categories nested in the body are mapped too: <c>SubmissionService</c> replaces them with the
    /// scoring model's, but an evaluation's create body stores the submissions nested in it as sent.
    /// </remarks>
    [MapperRequiredMapping(RequiredMappingStrategy.Source)]
    [MapperIgnoreSource(nameof(Submission.GroupId))]
    [MapperIgnoreSource(nameof(Submission.ScoreIsAnAverage))]
    public static partial SubmissionEntity ToEntity(this Submission source);

    private static ICollection<SubmissionCategory> MapCategories(ICollection<SubmissionCategoryEntity> source) =>
        source == null ? [] : source.Select(x => x.ToSubmissionCategory()).ToList();

    private static ICollection<SubmissionCategoryEntity> MapCategories(ICollection<SubmissionCategory> source) =>
        source == null ? [] : source.Select(x => x.ToEntity()).ToList();
}
