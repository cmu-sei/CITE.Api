// Copyright 2026 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.

using System;
using Cite.Api.Data.Models;
using Cite.Api.ViewModels;
using Riok.Mapperly.Abstractions;

namespace Cite.Api.Infrastructure.Mappings;

/// <summary>Team memberships' mappings.</summary>
[Mapper]
[UseStaticMapper(typeof(TeamRoleMapper))]
public static partial class TeamMembershipMapper
{
    /// <remarks><see cref="TeamMembership.Role"/> is read from whatever role the context has loaded, and is null otherwise.</remarks>
    public static partial TeamMembership ToTeamMembership(this TeamMembershipEntity source);

    /// <remarks>
    /// A null <see cref="TeamMembership.UserId"/> leaves the entity's empty one. The role is named by
    /// <see cref="TeamMembership.RoleId"/>; a body that also nests a role object is refused with a 500, as
    /// it was when no mapping from a role view model to a role entity existed.
    /// </remarks>
    [MapperRequiredMapping(RequiredMappingStrategy.Source)]
    [MapProperty(nameof(TeamMembership.Role), nameof(TeamMembershipEntity.Role), Use = nameof(RefuseRole))]
    public static partial TeamMembershipEntity ToEntity(this TeamMembership source);

    [UserMapping(Default = false)]
    private static TeamRoleEntity RefuseRole(TeamRole role) =>
        role == null ? null : throw new InvalidOperationException("A team membership names its role by roleId; a nested role is not mapped.");
}
