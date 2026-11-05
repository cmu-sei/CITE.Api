// Copyright 2026 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using Cite.Api.Data.Models;
using Cite.Api.ViewModels;
using Riok.Mapperly.Abstractions;

namespace Cite.Api.Infrastructure.Mappings;

/// <summary>Submission categories' mappings. A missing option list is an empty one in both directions.</summary>
[Mapper]
[UseStaticMapper(typeof(SubmissionOptionMapper))]
public static partial class SubmissionCategoryMapper
{
    public static partial SubmissionCategory ToSubmissionCategory(this SubmissionCategoryEntity source);

    /// <remarks>The options nested in the body, and their comments, are stored with the category.</remarks>
    [MapperRequiredMapping(RequiredMappingStrategy.Source)]
    public static partial SubmissionCategoryEntity ToEntity(this SubmissionCategory source);

    private static ICollection<SubmissionOption> MapOptions(ICollection<SubmissionOptionEntity> source) =>
        source == null ? [] : source.Select(x => x.ToSubmissionOption()).ToList();

    private static ICollection<SubmissionOptionEntity> MapOptions(ICollection<SubmissionOption> source) =>
        source == null ? [] : source.Select(x => x.ToEntity()).ToList();
}
