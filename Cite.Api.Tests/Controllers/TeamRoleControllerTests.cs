// Copyright 2026 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.

using System.Net;
using Cite.Api.Data.Enumerations;
using Cite.Api.Tests.Support;
using Cite.Api.ViewModels;

namespace Cite.Api.Tests.Controllers;

/// <summary><c>TeamRolesController</c>: the read-only team roles, open to every authenticated caller.</summary>
public class TeamRoleControllerTests(DatabaseFixture fixture, CiteAppFactory factory) : ApiTestBase(fixture, factory)
{
    /// <summary>A request with no identity is answered with a 401; the anonymous client is the case under test.</summary>
    [Fact]
    public async Task GetAll_without_an_identity_is_unauthorized()
    {
        await AssertStatus(HttpStatusCode.Unauthorized, await Client().GetAsync("api/team-roles", Ct));
    }

    /// <summary>No permission is checked: a caller holding no permission at all reads every team role.</summary>
    [Fact]
    public async Task GetAll_lists_the_four_seeded_roles_for_any_authenticated_caller()
    {
        var actor = await Actor().SeedAsync();

        var roles = await ReadAsync<List<TeamRole>>(await Client(actor).GetAsync("api/team-roles", Ct));

        Assert.Equal(["Contributor", "Member", "Owner", "Submitter"], roles.Select(x => x.Name).Order());
    }

    /// <summary>No permission is checked: a caller holding no permission at all reads a team role.</summary>
    [Fact]
    public async Task Get_returns_the_owner_role_to_any_authenticated_caller()
    {
        var actor = await Actor().SeedAsync();

        var role = await ReadAsync<TeamRole>(await Client(actor).GetAsync($"api/team-roles/{TestData.TeamRoles.Owner}", Ct));

        Assert.Contains(TeamPermission.ManageTeam, role.Permissions);
    }

    [Fact]
    public async Task Get_answers_an_unknown_role_with_not_found()
    {
        await AssertApiError(HttpStatusCode.NotFound, await RootClient.GetAsync($"api/team-roles/{Guid.NewGuid()}", Ct));
    }
}
