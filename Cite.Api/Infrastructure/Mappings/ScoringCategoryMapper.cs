// Copyright 2026 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using Cite.Api.Data.Models;
using Cite.Api.ViewModels;
using Riok.Mapperly.Abstractions;

namespace Cite.Api.Infrastructure.Mappings;

/// <summary>Scoring categories' mappings. A missing option list is an empty one in both directions.</summary>
[Mapper]
[UseStaticMapper(typeof(ScoringOptionMapper))]
public static partial class ScoringCategoryMapper
{
    /// <remarks>
    /// A category is scored exactly when <c>SubmissionService</c> would calculate its score. Callers that
    /// redact the equation do it after mapping, so <see cref="ScoringCategory.IsScored"/> still reflects the
    /// real one.
    /// </remarks>
    [MapPropertyFromSource(nameof(ScoringCategory.IsScored), Use = nameof(IsScored))]
    public static partial ScoringCategory ToScoringCategory(this ScoringCategoryEntity source);

    /// <remarks>The options nested in the body are stored with the category, as sent.</remarks>
    [MapperRequiredMapping(RequiredMappingStrategy.Source)]
    [MapperIgnoreSource(nameof(ScoringCategory.IsScored))]
    public static partial ScoringCategoryEntity ToEntity(this ScoringCategory source);

    [UserMapping(Default = false)]
    private static bool IsScored(ScoringCategoryEntity source) => !string.IsNullOrWhiteSpace(source.CalculationEquation);

    private static ICollection<ScoringOption> MapOptions(ICollection<ScoringOptionEntity> source) =>
        source == null ? [] : source.Select(x => x.ToScoringOption()).ToList();

    private static ICollection<ScoringOptionEntity> MapOptions(ICollection<ScoringOption> source) =>
        source == null ? [] : source.Select(x => x.ToEntity()).ToList();
}
