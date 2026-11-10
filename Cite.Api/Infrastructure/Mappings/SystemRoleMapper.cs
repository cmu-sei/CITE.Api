// Copyright 2026 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using Cite.Api.Data.Enumerations;
using Cite.Api.Data.Models;
using Cite.Api.ViewModels;
using Riok.Mapperly.Abstractions;

namespace Cite.Api.Infrastructure.Mappings;

/// <summary>
/// System roles' mappings. Missing permissions are an empty list in both directions, never null. The view
/// model has no description, so an update keeps the stored one.
/// </summary>
[Mapper]
public static partial class SystemRoleMapper
{
    public static partial SystemRole ToSystemRole(this SystemRoleEntity source);

    [MapperRequiredMapping(RequiredMappingStrategy.Source)]
    public static partial SystemRoleEntity ToEntity(this SystemRole source);

    /// <remarks>
    /// Writes the body's id onto the row as well, so a body whose id is not the route's (or has none)
    /// tries to change the key and fails the save.
    /// </remarks>
    [MapperRequiredMapping(RequiredMappingStrategy.Source)]
    public static partial void ApplyTo(this SystemRole source, SystemRoleEntity target);

    private static SystemPermission[] MapPermissions(List<SystemPermission> source) =>
        source == null ? [] : source.ToArray();

    private static List<SystemPermission> MapPermissions(SystemPermission[] source) =>
        source == null ? [] : source.ToList();
}
