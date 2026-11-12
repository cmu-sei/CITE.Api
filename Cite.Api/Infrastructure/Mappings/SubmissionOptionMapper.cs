// Copyright 2026 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using Cite.Api.Data.Models;
using Cite.Api.ViewModels;
using Riok.Mapperly.Abstractions;

namespace Cite.Api.Infrastructure.Mappings;

/// <summary>
/// Submission options' mappings. <see cref="SubmissionOption.SelectedCount"/> is only filled on an average
/// submission, by <c>SubmissionService</c>; it is never read from or written to the entity. A missing
/// comment list is an empty one in both directions.
/// </summary>
[Mapper]
[UseStaticMapper(typeof(SubmissionCommentMapper))]
public static partial class SubmissionOptionMapper
{
    [MapperIgnoreTarget(nameof(SubmissionOption.SelectedCount))]
    public static partial SubmissionOption ToSubmissionOption(this SubmissionOptionEntity source);

    /// <remarks>The comments nested in the body are stored with the option.</remarks>
    [MapperRequiredMapping(RequiredMappingStrategy.Source)]
    [MapperIgnoreSource(nameof(SubmissionOption.SelectedCount))]
    public static partial SubmissionOptionEntity ToEntity(this SubmissionOption source);

    private static ICollection<SubmissionComment> MapComments(ICollection<SubmissionCommentEntity> source) =>
        source == null ? [] : source.Select(x => x.ToSubmissionComment()).ToList();

    private static ICollection<SubmissionCommentEntity> MapComments(ICollection<SubmissionComment> source) =>
        source == null ? [] : source.Select(x => x.ToEntity()).ToList();
}
