// Copyright 2026 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.

using System.Net;
using Cite.Api.Data.Enumerations;
using Cite.Api.Tests.Support;
using Cite.Api.ViewModels;

namespace Cite.Api.Tests.Controllers;

/// <summary><c>ScoringModelRolesController</c>: the read-only scoring model roles, gated by ViewRoles.</summary>
public class ScoringModelRoleControllerTests(DatabaseFixture fixture, CiteAppFactory factory) : ApiTestBase(fixture, factory)
{
    /// <summary>A request with no identity is answered with a 401; the anonymous client is the case under test.</summary>
    [Fact]
    public async Task GetAll_without_an_identity_is_unauthorized()
    {
        await AssertStatus(HttpStatusCode.Unauthorized, await Client().GetAsync("api/scoringModel-roles", Ct));
    }

    [Fact]
    public async Task GetAll_lists_the_three_seeded_roles_for_a_caller_holding_ViewRoles()
    {
        var actor = await Actor().WithSystemPermissions(SystemPermission.ViewRoles).SeedAsync();

        var roles = await ReadAsync<List<ScoringModelRole>>(await Client(actor).GetAsync("api/scoringModel-roles", Ct));

        Assert.Equal(["Editor", "Owner", "Viewer"], roles.Select(x => x.Name).Order());
    }

    [Fact]
    public async Task GetAll_is_forbidden_for_a_caller_holding_only_ManageRoles()
    {
        var actor = await Actor().WithSystemPermissions(SystemPermission.ManageRoles).SeedAsync();

        await AssertStatus(HttpStatusCode.Forbidden, await Client(actor).GetAsync("api/scoringModel-roles", Ct));
    }

    [Fact]
    public async Task Get_returns_the_owner_role_to_a_caller_holding_ViewRoles()
    {
        var actor = await Actor().WithSystemPermissions(SystemPermission.ViewRoles).SeedAsync();

        var role = await ReadAsync<ScoringModelRole>(await Client(actor).GetAsync($"api/scoringModel-roles/{TestData.ScoringModelRoles.Owner}", Ct));

        Assert.True(role.AllPermissions);
    }

    [Fact]
    public async Task Get_is_forbidden_for_a_caller_holding_only_ManageRoles()
    {
        var actor = await Actor().WithSystemPermissions(SystemPermission.ManageRoles).SeedAsync();

        await AssertStatus(HttpStatusCode.Forbidden, await Client(actor).GetAsync($"api/scoringModel-roles/{TestData.ScoringModelRoles.Owner}", Ct));
    }

    [Fact]
    public async Task Get_answers_an_unknown_role_with_not_found()
    {
        await AssertApiError(HttpStatusCode.NotFound, await RootClient.GetAsync($"api/scoringModel-roles/{Guid.NewGuid()}", Ct));
    }
}
