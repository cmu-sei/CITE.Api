// Copyright 2026 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using Cite.Api.Data.Enumerations;
using Cite.Api.Data.Models;
using Cite.Api.ViewModels;
using Riok.Mapperly.Abstractions;

namespace Cite.Api.Infrastructure.Mappings;

/// <summary>Teams' in-memory mapping.</summary>
/// <remarks>
/// An in-memory map fills every member from whatever the context has loaded: the team type, the
/// memberships (each with its role) and the submissions are null or empty when not loaded. A projection
/// reads only some of them: see <see cref="TeamProjectionMapper"/> and <see cref="TeamWithMembershipsProjectionMapper"/>.
/// </remarks>
[Mapper]
[UseStaticMapper(typeof(TeamTypeMapper))]
[UseStaticMapper(typeof(TeamMembershipMapper))]
[UseStaticMapper(typeof(SubmissionMapper))]
public static partial class TeamMapper
{
    public static partial Team ToTeam(this TeamEntity source);

    private static ICollection<Submission> MapSubmissions(ICollection<SubmissionEntity> source) =>
        source == null ? [] : source.Select(x => x.ToSubmission()).ToList();

    private static ICollection<TeamMembership> MapMemberships(ICollection<TeamMembershipEntity> source) =>
        source == null ? [] : source.Select(x => x.ToTeamMembership()).ToList();
}

/// <summary>The projection of a team with its team type, without its memberships or submissions.</summary>
/// <remarks>
/// Each projection variant is a mapper of its own, because a projection takes the default mapping of
/// its element types and a mapper has one default per type pair.
/// </remarks>
[Mapper]
public static partial class TeamProjectionMapper
{
    /// <remarks><see cref="Team.Memberships"/> and <see cref="Team.Submissions"/> are left empty.</remarks>
    public static partial IQueryable<Team> ProjectToTeam(this IQueryable<TeamEntity> query);

    [MapperIgnoreTarget(nameof(Team.Memberships))]
    [MapperIgnoreTarget(nameof(Team.Submissions))]
    private static partial Team MapToTeam(TeamEntity source);

    // TeamTypeMapper owns this mapping. It is declared here because Mapperly 4.3 calls a
    // [UseStaticMapper] method inside a projection instead of inlining it.
    [IncludeMappingConfiguration(nameof(@TeamTypeMapper.ToTeamType))]
    private static partial TeamType MapToTeamType(TeamTypeEntity source);
}

/// <summary>The projection of a team with its team type and its memberships, each with its role, without its submissions.</summary>
[Mapper]
public static partial class TeamWithMembershipsProjectionMapper
{
    /// <remarks><see cref="Team.Submissions"/> is left empty.</remarks>
    public static partial IQueryable<Team> ProjectToTeamWithMemberships(this IQueryable<TeamEntity> query);

    [MapProperty(nameof(TeamEntity.Memberships), nameof(Team.Memberships), Use = nameof(MapMemberships))]
    [MapperIgnoreTarget(nameof(Team.Submissions))]
    private static partial Team MapToTeam(TeamEntity source);

    // The owners' mappings, declared here for the projection as in TeamProjectionMapper.
    [IncludeMappingConfiguration(nameof(@TeamTypeMapper.ToTeamType))]
    private static partial TeamType MapToTeamType(TeamTypeEntity source);

    [IncludeMappingConfiguration(nameof(@TeamMembershipMapper.ToTeamMembership))]
    private static partial TeamMembership MapToTeamMembership(TeamMembershipEntity source);

    [IncludeMappingConfiguration(nameof(@TeamRoleMapper.ToTeamRole))]
    private static partial TeamRole MapToTeamRole(TeamRoleEntity source);

    // Expression-bodied, so Mapperly inlines them into the projection.
    private static ICollection<TeamMembership> MapMemberships(ICollection<TeamMembershipEntity> memberships) =>
        memberships.Select(x => MapToTeamMembership(x)).ToList();

    // The Select makes EF Core read the permissions element by element (an unnest of the array column),
    // as AutoMapper's projection did, so a role stored without permissions is read with an empty list.
    // Without it the column is read as is, and a null array stays null.
    private static TeamPermission[] MapPermissions(List<TeamPermission> permissions) =>
        permissions.Select(x => x).ToArray();
}
