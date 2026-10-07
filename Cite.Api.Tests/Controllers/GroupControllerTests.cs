// Copyright 2026 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.

using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Cite.Api.Data.Enumerations;
using Cite.Api.Data.Models;
using Cite.Api.Tests.Support;
using Cite.Api.ViewModels;

namespace Cite.Api.Tests.Controllers;

/// <summary>
/// <c>GroupController</c>: groups and their memberships, gated by the system permissions ViewGroups and
/// ManageGroups alone.
/// </summary>
public class GroupControllerTests(DatabaseFixture fixture, CiteAppFactory factory) : ApiTestBase(fixture, factory)
{
    /// <summary>A request with no identity is answered with a 401; the anonymous client is the case under test.</summary>
    [Fact]
    public async Task GetAll_without_an_identity_is_unauthorized()
    {
        await AssertStatus(HttpStatusCode.Unauthorized, await Client().GetAsync("api/groups", Ct));
    }

    [Fact]
    public async Task GetAll_returns_every_group_to_a_caller_holding_ViewGroups()
    {
        var group = TestData.Group("Readers");
        await Seed(group);
        var actor = await Actor().WithSystemPermissions(SystemPermission.ViewGroups).SeedAsync();

        var groups = await ReadAsync<List<Group>>(await Client(actor).GetAsync("api/groups", Ct));

        Assert.Contains(groups, x => x.Id == group.Id && x.Name == "Readers");
    }

    [Fact]
    public async Task GetAll_is_forbidden_for_a_caller_holding_only_ManageGroups()
    {
        var actor = await Actor().WithSystemPermissions(SystemPermission.ManageGroups).SeedAsync();

        await AssertApiError(HttpStatusCode.Forbidden, await Client(actor).GetAsync("api/groups", Ct));
    }

    [Fact]
    public async Task Get_returns_the_group_to_a_caller_holding_ViewGroups()
    {
        var group = TestData.Group("Readers");
        await Seed(group);
        var actor = await Actor().WithSystemPermissions(SystemPermission.ViewGroups).SeedAsync();

        var read = await ReadAsync<Group>(await Client(actor).GetAsync($"api/groups/{group.Id}", Ct));

        Assert.Equal("Readers", read.Name);
    }

    [Fact]
    public async Task Get_is_forbidden_for_a_caller_holding_only_ManageGroups()
    {
        var group = TestData.Group();
        await Seed(group);
        var actor = await Actor().WithSystemPermissions(SystemPermission.ManageGroups).SeedAsync();

        await AssertStatus(HttpStatusCode.Forbidden, await Client(actor).GetAsync($"api/groups/{group.Id}", Ct));
    }

    [Fact]
    public async Task Create_persists_the_group_for_a_caller_holding_ManageGroups()
    {
        var actor = await Actor().WithSystemPermissions(SystemPermission.ManageGroups).SeedAsync();

        var response = await Client(actor).PostAsJsonAsync("api/groups", new { name = "Created Group" }, Ct);

        var created = await ReadAsync<Group>(response);
        await using var context = NewContext();
        Assert.Equal("Created Group", (await context.Groups.SingleAsync(x => x.Id == created.Id, Ct)).Name);
    }

    [Fact]
    public async Task Create_is_forbidden_for_a_caller_holding_only_ViewGroups()
    {
        var actor = await Actor().WithSystemPermissions(SystemPermission.ViewGroups).SeedAsync();

        var response = await Client(actor).PostAsJsonAsync("api/groups", new { name = "Refused Group" }, Ct);

        await AssertStatus(HttpStatusCode.Forbidden, response);
        await using var context = NewContext();
        Assert.False(await context.Groups.AnyAsync(x => x.Name == "Refused Group", Ct));
    }

    [Fact]
    public async Task Update_renames_the_group_for_a_caller_holding_ManageGroups()
    {
        var group = TestData.Group("Before");
        await Seed(group);
        var actor = await Actor().WithSystemPermissions(SystemPermission.ManageGroups).SeedAsync();

        var response = await Client(actor).PutAsJsonAsync($"api/groups/{group.Id}", new { id = group.Id, name = "After" }, Ct);

        await AssertStatus(HttpStatusCode.OK, response);
        await using var context = NewContext();
        Assert.Equal("After", (await context.Groups.SingleAsync(x => x.Id == group.Id, Ct)).Name);
    }

    [Fact]
    public async Task Update_is_forbidden_for_a_caller_holding_only_ViewGroups()
    {
        var group = TestData.Group("Before");
        await Seed(group);
        var actor = await Actor().WithSystemPermissions(SystemPermission.ViewGroups).SeedAsync();

        var response = await Client(actor).PutAsJsonAsync($"api/groups/{group.Id}", new { id = group.Id, name = "After" }, Ct);

        await AssertStatus(HttpStatusCode.Forbidden, response);
        await using var context = NewContext();
        Assert.Equal("Before", (await context.Groups.SingleAsync(x => x.Id == group.Id, Ct)).Name);
    }

    [Fact]
    public async Task Update_answers_an_unknown_group_with_not_found()
    {
        var id = Guid.NewGuid();

        var error = await AssertApiError(HttpStatusCode.NotFound,
            await RootClient.PutAsJsonAsync($"api/groups/{id}", new { id, name = "Nobody" }, Ct));

        Assert.Equal("Group not found", error.Title);
    }

    [Fact]
    public async Task Delete_removes_the_group_for_a_caller_holding_ManageGroups()
    {
        var group = TestData.Group();
        await Seed(group);
        var actor = await Actor().WithSystemPermissions(SystemPermission.ManageGroups).SeedAsync();

        await AssertStatus(HttpStatusCode.NoContent, await Client(actor).DeleteAsync($"api/groups/{group.Id}", Ct));

        await using var context = NewContext();
        Assert.False(await context.Groups.AnyAsync(x => x.Id == group.Id, Ct));
    }

    [Fact]
    public async Task Delete_is_forbidden_for_a_caller_holding_only_ViewGroups()
    {
        var group = TestData.Group();
        await Seed(group);
        var actor = await Actor().WithSystemPermissions(SystemPermission.ViewGroups).SeedAsync();

        await AssertStatus(HttpStatusCode.Forbidden, await Client(actor).DeleteAsync($"api/groups/{group.Id}", Ct));

        await using var context = NewContext();
        Assert.True(await context.Groups.AnyAsync(x => x.Id == group.Id, Ct));
    }

    [Fact]
    public async Task GetMemberships_lists_the_group_members_for_a_caller_holding_ViewGroups()
    {
        var (group, membership) = await SeedGroupWithMember();
        var actor = await Actor().WithSystemPermissions(SystemPermission.ViewGroups).SeedAsync();

        var memberships = await ReadAsync<List<GroupMembership>>(await Client(actor).GetAsync($"api/groups/{group.Id}/memberships", Ct));

        Assert.Equal(membership.UserId, Assert.Single(memberships).UserId);
    }

    [Fact]
    public async Task GetMemberships_is_forbidden_for_a_caller_holding_only_ManageGroups()
    {
        var (group, _) = await SeedGroupWithMember();
        var actor = await Actor().WithSystemPermissions(SystemPermission.ManageGroups).SeedAsync();

        await AssertStatus(HttpStatusCode.Forbidden, await Client(actor).GetAsync($"api/groups/{group.Id}/memberships", Ct));
    }

    [Fact]
    public async Task GetMembership_returns_the_membership_to_a_caller_holding_ViewGroups()
    {
        var (group, membership) = await SeedGroupWithMember();
        var actor = await Actor().WithSystemPermissions(SystemPermission.ViewGroups).SeedAsync();

        var read = await ReadAsync<GroupMembership>(await Client(actor).GetAsync($"api/groups/memberships/{membership.Id}", Ct));

        Assert.Equal(group.Id, read.GroupId);
    }

    [Fact]
    public async Task GetMembership_is_forbidden_for_a_caller_holding_only_ManageGroups()
    {
        var (_, membership) = await SeedGroupWithMember();
        var actor = await Actor().WithSystemPermissions(SystemPermission.ManageGroups).SeedAsync();

        await AssertStatus(HttpStatusCode.Forbidden, await Client(actor).GetAsync($"api/groups/memberships/{membership.Id}", Ct));
    }

    [Fact]
    public async Task CreateMembership_adds_the_user_for_a_caller_holding_ManageGroups()
    {
        var group = TestData.Group();
        var user = TestData.User();
        await Seed(group, user);
        var actor = await Actor().WithSystemPermissions(SystemPermission.ManageGroups).SeedAsync();

        var response = await Client(actor).PostAsJsonAsync($"api/groups/{group.Id}/memberships", new { groupId = group.Id, userId = user.Id }, Ct);

        await AssertStatus(HttpStatusCode.Created, response);
        await using var context = NewContext();
        Assert.True(await context.GroupMemberships.AnyAsync(x => x.GroupId == group.Id && x.UserId == user.Id, Ct));
    }

    [Fact]
    public async Task CreateMembership_is_forbidden_for_a_caller_holding_only_ViewGroups()
    {
        var group = TestData.Group();
        var user = TestData.User();
        await Seed(group, user);
        var actor = await Actor().WithSystemPermissions(SystemPermission.ViewGroups).SeedAsync();

        var response = await Client(actor).PostAsJsonAsync($"api/groups/{group.Id}/memberships", new { groupId = group.Id, userId = user.Id }, Ct);

        await AssertStatus(HttpStatusCode.Forbidden, response);
        await using var context = NewContext();
        Assert.False(await context.GroupMemberships.AnyAsync(x => x.GroupId == group.Id, Ct));
    }

    /// <summary>The membership is created in the group the body names, whatever group the route names.</summary>
    [Fact]
    public async Task CreateMembership_adds_the_user_to_the_group_in_the_body_rather_than_the_route()
    {
        var routeGroup = TestData.Group("Route Group");
        var bodyGroup = TestData.Group("Body Group");
        var user = TestData.User();
        await Seed(routeGroup, bodyGroup, user);

        var response = await RootClient.PostAsJsonAsync($"api/groups/{routeGroup.Id}/memberships", new { groupId = bodyGroup.Id, userId = user.Id }, Ct);

        await AssertStatus(HttpStatusCode.Created, response);
        await using var context = NewContext();
        Assert.Equal(bodyGroup.Id, (await context.GroupMemberships.SingleAsync(x => x.UserId == user.Id, Ct)).GroupId);
    }

    /// <summary>Adding a user to a group they are already in is answered with a 500.</summary>
    [Fact]
    public async Task CreateMembership_answers_a_duplicate_member_with_a_server_error()
    {
        var (group, membership) = await SeedGroupWithMember();

        var response = await RootClient.PostAsJsonAsync($"api/groups/{group.Id}/memberships", new { groupId = group.Id, userId = membership.UserId }, Ct);

        var error = await AssertApiError(HttpStatusCode.InternalServerError, response);
        Assert.StartsWith("An error occurred while saving the entity changes.", error.Detail);
    }

    [Fact]
    public async Task DeleteMembership_removes_the_membership_for_a_caller_holding_ManageGroups()
    {
        var (_, membership) = await SeedGroupWithMember();
        var actor = await Actor().WithSystemPermissions(SystemPermission.ManageGroups).SeedAsync();

        await AssertStatus(HttpStatusCode.NoContent, await Client(actor).DeleteAsync($"api/groups/memberships/{membership.Id}", Ct));

        await using var context = NewContext();
        Assert.False(await context.GroupMemberships.AnyAsync(x => x.Id == membership.Id, Ct));
    }

    [Fact]
    public async Task DeleteMembership_is_forbidden_for_a_caller_holding_only_ViewGroups()
    {
        var (_, membership) = await SeedGroupWithMember();
        var actor = await Actor().WithSystemPermissions(SystemPermission.ViewGroups).SeedAsync();

        await AssertStatus(HttpStatusCode.Forbidden, await Client(actor).DeleteAsync($"api/groups/memberships/{membership.Id}", Ct));

        await using var context = NewContext();
        Assert.True(await context.GroupMemberships.AnyAsync(x => x.Id == membership.Id, Ct));
    }

    private async Task<(GroupEntity Group, GroupMembershipEntity Membership)> SeedGroupWithMember()
    {
        var group = TestData.Group();
        var user = TestData.User();
        var membership = TestData.GroupMembership(group.Id, user.Id);
        await Seed(group, user, membership);

        return (group, membership);
    }
}
