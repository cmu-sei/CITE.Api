// Copyright 2026 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.

using Cite.Api.Data.Models;
using Cite.Api.ViewModels;
using Riok.Mapperly.Abstractions;

namespace Cite.Api.Infrastructure.Mappings;

/// <summary>Scoring model memberships' mappings.</summary>
[Mapper]
public static partial class ScoringModelMembershipMapper
{
    public static partial ScoringModelMembership ToScoringModelMembership(this ScoringModelMembershipEntity source);

    [MapperRequiredMapping(RequiredMappingStrategy.Source)]
    public static partial ScoringModelMembershipEntity ToEntity(this ScoringModelMembership source);
}
