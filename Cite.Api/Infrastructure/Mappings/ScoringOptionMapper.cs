// Copyright 2026 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.

using Cite.Api.Data.Models;
using Cite.Api.ViewModels;
using Riok.Mapperly.Abstractions;

namespace Cite.Api.Infrastructure.Mappings;

/// <summary>Scoring options' mappings.</summary>
[Mapper]
public static partial class ScoringOptionMapper
{
    public static partial ScoringOption ToScoringOption(this ScoringOptionEntity source);

    [MapperRequiredMapping(RequiredMappingStrategy.Source)]
    public static partial ScoringOptionEntity ToEntity(this ScoringOption source);
}
