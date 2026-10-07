// Copyright 2026 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.

using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Cite.Api.Data.Enumerations;
using Cite.Api.Tests.Support;
using Cite.Api.ViewModels;

namespace Cite.Api.Tests.Controllers;

/// <summary><c>SystemRoleController</c>: system roles, gated by ViewRoles and ManageRoles.</summary>
public class SystemRoleControllerTests(DatabaseFixture fixture, CiteAppFactory factory) : ApiTestBase(fixture, factory)
{
    /// <summary>A request with no identity is answered with a 401; the anonymous client is the case under test.</summary>
    [Fact]
    public async Task GetAll_without_an_identity_is_unauthorized()
    {
        await AssertStatus(HttpStatusCode.Unauthorized, await Client().GetAsync("api/system-roles", Ct));
    }

    [Fact]
    public async Task GetAll_lists_the_seeded_roles_for_a_caller_holding_ViewRoles()
    {
        var actor = await Actor().WithSystemPermissions(SystemPermission.ViewRoles).SeedAsync();

        var roles = await ReadAsync<List<SystemRole>>(await Client(actor).GetAsync("api/system-roles", Ct));

        Assert.Contains(roles, x => x.Id == TestData.Roles.Administrator && x.AllPermissions && x.Immutable);
        Assert.Contains(roles, x => x.Id == TestData.Roles.ContentDeveloper);
        Assert.Contains(roles, x => x.Id == TestData.Roles.Observer);
    }

    [Fact]
    public async Task GetAll_is_forbidden_for_a_caller_holding_only_ManageRoles()
    {
        var actor = await Actor().WithSystemPermissions(SystemPermission.ManageRoles).SeedAsync();

        await AssertStatus(HttpStatusCode.Forbidden, await Client(actor).GetAsync("api/system-roles", Ct));
    }

    [Fact]
    public async Task Get_returns_the_role_to_a_caller_holding_ViewRoles()
    {
        var actor = await Actor().WithSystemPermissions(SystemPermission.ViewRoles).SeedAsync();

        var role = await ReadAsync<SystemRole>(await Client(actor).GetAsync($"api/system-roles/{TestData.Roles.Observer}", Ct));

        Assert.Equal("Observer", role.Name);
        Assert.All(role.Permissions, x => Assert.StartsWith("View", x.ToString()));
    }

    [Fact]
    public async Task Get_is_forbidden_for_a_caller_holding_only_ManageRoles()
    {
        var actor = await Actor().WithSystemPermissions(SystemPermission.ManageRoles).SeedAsync();

        await AssertStatus(HttpStatusCode.Forbidden, await Client(actor).GetAsync($"api/system-roles/{TestData.Roles.Observer}", Ct));
    }

    [Fact]
    public async Task Create_persists_the_role_and_its_permissions_for_a_caller_holding_ManageRoles()
    {
        var actor = await Actor().WithSystemPermissions(SystemPermission.ManageRoles).SeedAsync();

        var response = await Client(actor).PostAsJsonAsync("api/system-roles",
            new { name = "Scorer", permissions = new[] { "ViewEvaluations", "ExecuteEvaluations" } }, Ct);

        var created = await ReadAsync<SystemRole>(response);
        await AssertStatus(HttpStatusCode.Created, response);
        await using var context = NewContext();
        var stored = await context.SystemRoles.SingleAsync(x => x.Id == created.Id, Ct);
        Assert.Equal("Scorer", stored.Name);
        Assert.Equal([SystemPermission.ViewEvaluations, SystemPermission.ExecuteEvaluations], stored.Permissions);
    }

    [Fact]
    public async Task Create_is_forbidden_for_a_caller_holding_only_ViewRoles()
    {
        var actor = await Actor().WithSystemPermissions(SystemPermission.ViewRoles).SeedAsync();

        var response = await Client(actor).PostAsJsonAsync("api/system-roles", new { name = "Refused Role", permissions = Array.Empty<string>() }, Ct);

        await AssertStatus(HttpStatusCode.Forbidden, response);
        await using var context = NewContext();
        Assert.False(await context.SystemRoles.AnyAsync(x => x.Name == "Refused Role", Ct));
    }

    /// <summary>A role name another role already has is answered with a 500.</summary>
    [Fact]
    public async Task Create_answers_a_name_that_is_already_taken_with_a_server_error()
    {
        var response = await RootClient.PostAsJsonAsync("api/system-roles", new { name = "Observer", permissions = Array.Empty<string>() }, Ct);

        var error = await AssertApiError(HttpStatusCode.InternalServerError, response);
        Assert.StartsWith("An error occurred while saving the entity changes.", error.Detail);
    }

    /// <summary>A permission number outside SystemPermission is stored as given.</summary>
    [Fact]
    public async Task Create_stores_a_permission_value_outside_the_enum()
    {
        var response = await RootClient.PostAsJsonAsync("api/system-roles", new { name = "Out Of Range", permissions = new[] { 999 } }, Ct);

        var created = await ReadAsync<SystemRole>(response);
        await using var context = NewContext();
        Assert.Equal([(SystemPermission)999], (await context.SystemRoles.SingleAsync(x => x.Id == created.Id, Ct)).Permissions);
    }

    [Fact]
    public async Task Update_changes_the_permissions_for_a_caller_holding_ManageRoles()
    {
        var role = TestData.SystemRole(permissions: [SystemPermission.ViewUsers]);
        await Seed(role);
        var actor = await Actor().WithSystemPermissions(SystemPermission.ManageRoles).SeedAsync();

        var response = await Client(actor).PutAsJsonAsync($"api/system-roles/{role.Id}",
            new { id = role.Id, name = role.Name, permissions = new[] { "ManageUsers" } }, Ct);

        await AssertStatus(HttpStatusCode.OK, response);
        await using var context = NewContext();
        Assert.Equal([SystemPermission.ManageUsers], (await context.SystemRoles.SingleAsync(x => x.Id == role.Id, Ct)).Permissions);
    }

    [Fact]
    public async Task Update_is_forbidden_for_a_caller_holding_only_ViewRoles()
    {
        var role = TestData.SystemRole(permissions: [SystemPermission.ViewUsers]);
        await Seed(role);
        var actor = await Actor().WithSystemPermissions(SystemPermission.ViewRoles).SeedAsync();

        var response = await Client(actor).PutAsJsonAsync($"api/system-roles/{role.Id}",
            new { id = role.Id, name = role.Name, permissions = new[] { "ManageUsers" } }, Ct);

        await AssertStatus(HttpStatusCode.Forbidden, response);
        await using var context = NewContext();
        Assert.Equal([SystemPermission.ViewUsers], (await context.SystemRoles.SingleAsync(x => x.Id == role.Id, Ct)).Permissions);
    }

    /// <summary>The seeded Administrator role, marked Immutable, accepts an edit that takes its permissions away.</summary>
    [Fact]
    public async Task Update_changes_the_immutable_administrator_role()
    {
        var response = await RootClient.PutAsJsonAsync($"api/system-roles/{TestData.Roles.Administrator}",
            new { id = TestData.Roles.Administrator, name = "Administrator", allPermissions = false, immutable = true, permissions = Array.Empty<string>() }, Ct);

        await AssertStatus(HttpStatusCode.OK, response);
        await using var context = NewContext();
        Assert.False((await context.SystemRoles.SingleAsync(x => x.Id == TestData.Roles.Administrator, Ct)).AllPermissions);
    }

    [Fact]
    public async Task Update_answers_an_unknown_role_with_not_found()
    {
        var id = Guid.NewGuid();

        var response = await RootClient.PutAsJsonAsync($"api/system-roles/{id}", new { id, name = "Nobody", permissions = Array.Empty<string>() }, Ct);

        await AssertApiError(HttpStatusCode.NotFound, response);
    }

    [Fact]
    public async Task Delete_removes_an_unassigned_role_for_a_caller_holding_ManageRoles()
    {
        var role = TestData.SystemRole();
        await Seed(role);
        var actor = await Actor().WithSystemPermissions(SystemPermission.ManageRoles).SeedAsync();

        await AssertStatus(HttpStatusCode.NoContent, await Client(actor).DeleteAsync($"api/system-roles/{role.Id}", Ct));

        await using var context = NewContext();
        Assert.False(await context.SystemRoles.AnyAsync(x => x.Id == role.Id, Ct));
    }

    [Fact]
    public async Task Delete_is_forbidden_for_a_caller_holding_only_ViewRoles()
    {
        var role = TestData.SystemRole();
        await Seed(role);
        var actor = await Actor().WithSystemPermissions(SystemPermission.ViewRoles).SeedAsync();

        await AssertStatus(HttpStatusCode.Forbidden, await Client(actor).DeleteAsync($"api/system-roles/{role.Id}", Ct));

        await using var context = NewContext();
        Assert.True(await context.SystemRoles.AnyAsync(x => x.Id == role.Id, Ct));
    }

    /// <summary>Deleting a role a user still holds is answered with a 500, and the role stays.</summary>
    [Fact]
    public async Task Delete_answers_a_role_a_user_still_holds_with_a_server_error()
    {
        var role = TestData.SystemRole();
        await Seed(role, TestData.User(roleId: role.Id));

        var response = await RootClient.DeleteAsync($"api/system-roles/{role.Id}", Ct);

        var error = await AssertApiError(HttpStatusCode.InternalServerError, response);
        Assert.StartsWith("An error occurred while saving the entity changes.", error.Detail);
    }
}
