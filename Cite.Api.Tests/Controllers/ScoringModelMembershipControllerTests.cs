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
/// <c>ScoringModelMembershipsController</c>: memberships are read with ViewScoringModel and managed with
/// ManageScoringModel on their scoring model, or the matching system permissions.
/// </summary>
public class ScoringModelMembershipControllerTests(DatabaseFixture fixture, CiteAppFactory factory) : ApiTestBase(fixture, factory)
{
    /// <summary>A request with no identity is answered with a 401; the anonymous client is the case under test.</summary>
    [Fact]
    public async Task GetAll_without_an_identity_is_unauthorized()
    {
        await AssertStatus(HttpStatusCode.Unauthorized, await Client().GetAsync($"api/scoringModels/{Guid.NewGuid()}/memberships", Ct));
    }

    [Fact]
    public async Task GetAll_lists_the_memberships_to_a_caller_holding_ViewScoringModel_on_it()
    {
        var (model, membership) = await SeedWithMember();
        var actor = await Actor().OnScoringModel(model.Id, permissions: [ScoringModelPermission.ViewScoringModel]).SeedAsync();

        var memberships = await ReadAsync<List<ScoringModelMembership>>(await Client(actor).GetAsync($"api/scoringModels/{model.Id}/memberships", Ct));

        Assert.Contains(memberships, x => x.Id == membership.Id);
    }

    [Fact]
    public async Task GetAll_is_forbidden_for_a_caller_holding_ViewScoringModel_only_on_another_scoring_model()
    {
        var (model, _) = await SeedWithMember();
        var actor = await Actor().OnNewScoringModel(ScoringModelPermission.ViewScoringModel).SeedAsync();

        await AssertApiError(HttpStatusCode.Forbidden, await Client(actor).GetAsync($"api/scoringModels/{model.Id}/memberships", Ct));
    }

    [Fact]
    public async Task Get_returns_the_membership_to_a_caller_holding_ViewScoringModels()
    {
        var (_, membership) = await SeedWithMember();
        var actor = await Actor().WithSystemPermissions(SystemPermission.ViewScoringModels).SeedAsync();

        var read = await ReadAsync<ScoringModelMembership>(await Client(actor).GetAsync($"api/scoringModels/memberships/{membership.Id}", Ct));

        Assert.Equal(membership.UserId, read.UserId);
    }

    [Fact]
    public async Task Get_is_forbidden_for_a_caller_holding_only_EditScoringModel_on_its_scoring_model()
    {
        var (model, membership) = await SeedWithMember();
        var actor = await Actor().OnScoringModel(model.Id, permissions: [ScoringModelPermission.EditScoringModel]).SeedAsync();

        await AssertStatus(HttpStatusCode.Forbidden, await Client(actor).GetAsync($"api/scoringModels/memberships/{membership.Id}", Ct));
    }

    [Fact]
    public async Task CreateMembership_adds_the_user_with_a_role_granting_ManageScoringModel_for_a_caller_holding_ManageScoringModel_on_it()
    {
        var model = TestData.ScoringModel();
        var user = TestData.User();
        var managerRole = TestData.ScoringModelRole(ScoringModelPermission.ManageScoringModel);
        await Seed(model, user, managerRole);
        var actor = await Actor().OnScoringModel(model.Id, permissions: [ScoringModelPermission.ManageScoringModel]).SeedAsync();

        var response = await Client(actor).PostAsJsonAsync($"api/scoringModels/{model.Id}/memberships",
            new { scoringModelId = model.Id, userId = user.Id, roleId = managerRole.Id }, Ct);

        await AssertStatus(HttpStatusCode.Created, response);
        await using var context = NewContext();
        Assert.Equal(managerRole.Id, (await context.ScoringModelMemberships.SingleAsync(x => x.ScoringModelId == model.Id && x.UserId == user.Id, Ct)).RoleId);
    }

    /// <summary>A caller holding only ManageScoringModel gives another user the Owner role, which holds every scoring model permission.</summary>
    [Fact]
    public async Task CreateMembership_lets_a_caller_holding_only_ManageScoringModel_grant_the_Owner_role()
    {
        var model = TestData.ScoringModel();
        var user = TestData.User();
        await Seed(model, user);
        var actor = await Actor().OnScoringModel(model.Id, permissions: [ScoringModelPermission.ManageScoringModel]).SeedAsync();

        var response = await Client(actor).PostAsJsonAsync($"api/scoringModels/{model.Id}/memberships",
            new { scoringModelId = model.Id, userId = user.Id, roleId = TestData.ScoringModelRoles.Owner }, Ct);

        await AssertStatus(HttpStatusCode.Created, response);
        await using var context = NewContext();
        Assert.Equal(TestData.ScoringModelRoles.Owner, (await context.ScoringModelMemberships.SingleAsync(x => x.ScoringModelId == model.Id && x.UserId == user.Id, Ct)).RoleId);
    }

    [Fact]
    public async Task CreateMembership_is_forbidden_for_a_caller_holding_only_EditScoringModel()
    {
        var model = TestData.ScoringModel();
        var user = TestData.User();
        await Seed(model, user);
        var actor = await Actor().OnScoringModel(model.Id, permissions: [ScoringModelPermission.EditScoringModel]).SeedAsync();

        var response = await Client(actor).PostAsJsonAsync($"api/scoringModels/{model.Id}/memberships",
            new { scoringModelId = model.Id, userId = user.Id, roleId = TestData.ScoringModelRoles.Editor }, Ct);

        await AssertStatus(HttpStatusCode.Forbidden, response);
        await using var context = NewContext();
        Assert.False(await context.ScoringModelMemberships.AnyAsync(x => x.UserId == user.Id, Ct));
    }

    [Fact]
    public async Task CreateMembership_is_forbidden_for_a_caller_holding_ManageScoringModel_only_on_another_scoring_model()
    {
        var model = TestData.ScoringModel();
        var user = TestData.User();
        await Seed(model, user);
        var actor = await Actor().OnNewScoringModel(ScoringModelPermission.ManageScoringModel).SeedAsync();

        await AssertStatus(HttpStatusCode.Forbidden, await Client(actor).PostAsJsonAsync($"api/scoringModels/{model.Id}/memberships",
            new { scoringModelId = model.Id, userId = user.Id, roleId = TestData.ScoringModelRoles.Editor }, Ct));
    }

    /// <summary>A body naming another scoring model than the route is refused with a 500.</summary>
    [Fact]
    public async Task CreateMembership_answers_a_body_for_another_scoring_model_with_a_server_error()
    {
        var model = TestData.ScoringModel();
        var other = TestData.ScoringModel("Other");
        var user = TestData.User();
        await Seed(model, other, user);

        var response = await RootClient.PostAsJsonAsync($"api/scoringModels/{model.Id}/memberships",
            new { scoringModelId = other.Id, userId = user.Id, roleId = TestData.ScoringModelRoles.Editor }, Ct);

        var error = await AssertApiError(HttpStatusCode.InternalServerError, response);
        Assert.Equal("The ScoringModelId of the membership must match the ScoringModelId of the URL.", error.Detail);
    }

    /// <summary>A second membership for a user already on the scoring model is stored beside the first.</summary>
    [Fact]
    public async Task CreateMembership_stores_a_second_membership_for_a_user_already_on_the_scoring_model()
    {
        var (model, membership) = await SeedWithMember();

        var response = await RootClient.PostAsJsonAsync($"api/scoringModels/{model.Id}/memberships",
            new { scoringModelId = model.Id, userId = membership.UserId, roleId = TestData.ScoringModelRoles.Owner }, Ct);

        await AssertStatus(HttpStatusCode.Created, response);
        await using var context = NewContext();
        Assert.Equal(2, await context.ScoringModelMemberships.CountAsync(x => x.ScoringModelId == model.Id && x.UserId == membership.UserId, Ct));
    }

    [Fact]
    public async Task Update_changes_the_role_to_one_granting_ManageScoringModel_for_a_caller_holding_ManageScoringModel_on_its_scoring_model()
    {
        var (model, membership) = await SeedWithMember();
        var managerRole = TestData.ScoringModelRole(ScoringModelPermission.ManageScoringModel);
        await Seed(managerRole);
        var actor = await Actor().OnScoringModel(model.Id, permissions: [ScoringModelPermission.ManageScoringModel]).SeedAsync();

        var response = await Client(actor).PutAsJsonAsync($"api/scoringModels/memberships/{membership.Id}",
            new { id = membership.Id, scoringModelId = model.Id, userId = membership.UserId, roleId = managerRole.Id }, Ct);

        await AssertStatus(HttpStatusCode.OK, response);
        await using var context = NewContext();
        Assert.Equal(managerRole.Id, (await context.ScoringModelMemberships.SingleAsync(x => x.Id == membership.Id, Ct)).RoleId);
    }

    // Same case as CreateMembership_lets_a_caller_holding_only_ManageScoringModel_grant_the_Owner_role.
    [Fact]
    public async Task Update_lets_a_caller_holding_only_ManageScoringModel_grant_the_Owner_role()
    {
        var (model, membership) = await SeedWithMember();
        var actor = await Actor().OnScoringModel(model.Id, permissions: [ScoringModelPermission.ManageScoringModel]).SeedAsync();

        var response = await Client(actor).PutAsJsonAsync($"api/scoringModels/memberships/{membership.Id}",
            new { id = membership.Id, scoringModelId = model.Id, userId = membership.UserId, roleId = TestData.ScoringModelRoles.Owner }, Ct);

        await AssertStatus(HttpStatusCode.OK, response);
        await using var context = NewContext();
        Assert.Equal(TestData.ScoringModelRoles.Owner, (await context.ScoringModelMemberships.SingleAsync(x => x.Id == membership.Id, Ct)).RoleId);
    }

    [Fact]
    public async Task Update_is_forbidden_for_a_caller_holding_ManageScoringModel_only_on_another_scoring_model()
    {
        var (model, membership) = await SeedWithMember();
        var actor = await Actor().OnNewScoringModel(ScoringModelPermission.ManageScoringModel).SeedAsync();

        var response = await Client(actor).PutAsJsonAsync($"api/scoringModels/memberships/{membership.Id}",
            new { id = membership.Id, scoringModelId = model.Id, userId = membership.UserId, roleId = TestData.ScoringModelRoles.Owner }, Ct);

        await AssertStatus(HttpStatusCode.Forbidden, response);
        await using var context = NewContext();
        Assert.Equal(TestData.ScoringModelRoles.Editor, (await context.ScoringModelMemberships.SingleAsync(x => x.Id == membership.Id, Ct)).RoleId);
    }

    [Fact]
    public async Task DeleteMembership_removes_the_membership_for_a_caller_holding_ManageScoringModels()
    {
        var (_, membership) = await SeedWithMember();
        var actor = await Actor().WithSystemPermissions(SystemPermission.ManageScoringModels).SeedAsync();

        await AssertStatus(HttpStatusCode.NoContent, await Client(actor).DeleteAsync($"api/scoringModels/memberships/{membership.Id}", Ct));

        await using var context = NewContext();
        Assert.False(await context.ScoringModelMemberships.AnyAsync(x => x.Id == membership.Id, Ct));
    }

    [Fact]
    public async Task DeleteMembership_is_forbidden_for_a_caller_holding_only_EditScoringModels()
    {
        var (_, membership) = await SeedWithMember();
        var actor = await Actor().WithSystemPermissions(SystemPermission.EditScoringModels).SeedAsync();

        await AssertStatus(HttpStatusCode.Forbidden, await Client(actor).DeleteAsync($"api/scoringModels/memberships/{membership.Id}", Ct));

        await using var context = NewContext();
        Assert.True(await context.ScoringModelMemberships.AnyAsync(x => x.Id == membership.Id, Ct));
    }

    private async Task<(ScoringModelEntity Model, ScoringModelMembershipEntity Membership)> SeedWithMember()
    {
        var model = TestData.ScoringModel();
        var user = TestData.User(name: "Member");
        var membership = TestData.ScoringModelMembership(model.Id, user.Id, TestData.ScoringModelRoles.Editor);
        await Seed(model, user, membership);

        return (model, membership);
    }
}
