// Copyright 2026 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.

using Cite.Api.Data.Models;
using Cite.Api.ViewModels;
using Riok.Mapperly.Abstractions;

namespace Cite.Api.Infrastructure.Mappings;

/// <summary>Team types' mappings.</summary>
[Mapper]
public static partial class TeamTypeMapper
{
    public static partial TeamType ToTeamType(this TeamTypeEntity source);

    /// <remarks>Every member is written, the audit fields included; <c>CiteContext</c> adjusts them as it saves.</remarks>
    [MapperRequiredMapping(RequiredMappingStrategy.Source)]
    public static partial TeamTypeEntity ToEntity(this TeamType source);

    /// <remarks>
    /// Writes the body's id onto the row as well, so a body whose id is not the route's (or has none)
    /// tries to change the key and fails the save.
    /// </remarks>
    [MapperRequiredMapping(RequiredMappingStrategy.Source)]
    public static partial void ApplyTo(this TeamType source, TeamTypeEntity target);
}
