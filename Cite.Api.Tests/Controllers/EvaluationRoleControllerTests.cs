// Copyright 2026 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.

using System.Net;
using Cite.Api.Data.Enumerations;
using Cite.Api.Tests.Support;
using Cite.Api.ViewModels;

namespace Cite.Api.Tests.Controllers;

/// <summary><c>EvaluationRolesController</c>: the read-only evaluation roles, gated by ViewRoles.</summary>
public class EvaluationRoleControllerTests(DatabaseFixture fixture, CiteAppFactory factory) : ApiTestBase(fixture, factory)
{
    /// <summary>A request with no identity is answered with a 401; the anonymous client is the case under test.</summary>
    [Fact]
    public async Task GetAll_without_an_identity_is_unauthorized()
    {
        await AssertStatus(HttpStatusCode.Unauthorized, await Client().GetAsync("api/evaluation-roles", Ct));
    }

    [Fact]
    public async Task GetAll_lists_the_seven_seeded_roles_for_a_caller_holding_ViewRoles()
    {
        var actor = await Actor().WithSystemPermissions(SystemPermission.ViewRoles).SeedAsync();

        var roles = await ReadAsync<List<EvaluationRole>>(await Client(actor).GetAsync("api/evaluation-roles", Ct));

        Assert.Equal(["Advancer", "Editor", "Facilitator", "Member", "Observer", "Owner", "Viewer"], roles.Select(x => x.Name).Order());
    }

    [Fact]
    public async Task GetAll_is_forbidden_for_a_caller_holding_only_ManageRoles()
    {
        var actor = await Actor().WithSystemPermissions(SystemPermission.ManageRoles).SeedAsync();

        await AssertStatus(HttpStatusCode.Forbidden, await Client(actor).GetAsync("api/evaluation-roles", Ct));
    }

    [Fact]
    public async Task Get_returns_the_owner_role_to_a_caller_holding_ViewRoles()
    {
        var actor = await Actor().WithSystemPermissions(SystemPermission.ViewRoles).SeedAsync();

        var role = await ReadAsync<EvaluationRole>(await Client(actor).GetAsync($"api/evaluation-roles/{TestData.EvaluationRoles.Owner}", Ct));

        Assert.True(role.AllPermissions);
    }

    [Fact]
    public async Task Get_is_forbidden_for_a_caller_holding_only_ManageRoles()
    {
        var actor = await Actor().WithSystemPermissions(SystemPermission.ManageRoles).SeedAsync();

        await AssertStatus(HttpStatusCode.Forbidden, await Client(actor).GetAsync($"api/evaluation-roles/{TestData.EvaluationRoles.Owner}", Ct));
    }

    [Fact]
    public async Task Get_answers_an_unknown_role_with_not_found()
    {
        await AssertApiError(HttpStatusCode.NotFound, await RootClient.GetAsync($"api/evaluation-roles/{Guid.NewGuid()}", Ct));
    }
}
