// Copyright 2026 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.

using Cite.Api.Data.Models;
using Cite.Api.ViewModels;
using Riok.Mapperly.Abstractions;

namespace Cite.Api.Infrastructure.Mappings;

/// <summary>Groups' mappings. The view model has no description, so an update keeps the stored one.</summary>
[Mapper]
public static partial class GroupMapper
{
    public static partial Group ToGroup(this GroupEntity source);

    [MapperRequiredMapping(RequiredMappingStrategy.Source)]
    public static partial GroupEntity ToEntity(this Group source);

    /// <remarks>
    /// Writes the body's id onto the row as well, so a body whose id is not the route's (or has none)
    /// tries to change the key and fails the save.
    /// </remarks>
    [MapperRequiredMapping(RequiredMappingStrategy.Source)]
    public static partial void ApplyTo(this Group source, GroupEntity target);
}
