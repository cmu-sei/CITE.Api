// Copyright 2026 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using Cite.Api.Data.Enumerations;
using Cite.Api.Data.Models;
using Cite.Api.ViewModels;
using Riok.Mapperly.Abstractions;

namespace Cite.Api.Infrastructure.Mappings;

/// <summary>Team roles' mappings. A role stored without permissions is read with an empty list.</summary>
[Mapper]
public static partial class TeamRoleMapper
{
    public static partial TeamRole ToTeamRole(this TeamRoleEntity source);

    private static TeamPermission[] MapPermissions(List<TeamPermission> source) =>
        source == null ? [] : source.ToArray();
}
