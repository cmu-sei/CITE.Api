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
/// <c>EvaluationMembershipsController</c>: memberships are read with ViewEvaluation and managed with
/// ManageEvaluation on their evaluation, or the matching system permissions.
/// </summary>
public class EvaluationMembershipControllerTests(DatabaseFixture fixture, CiteAppFactory factory) : ApiTestBase(fixture, factory)
{
    /// <summary>A request with no identity is answered with a 401; the anonymous client is the case under test.</summary>
    [Fact]
    public async Task GetAll_without_an_identity_is_unauthorized()
    {
        await AssertStatus(HttpStatusCode.Unauthorized, await Client().GetAsync($"api/evaluations/{Guid.NewGuid()}/memberships", Ct));
    }

    [Fact]
    public async Task GetAll_lists_the_memberships_to_a_caller_holding_ViewEvaluation_on_it()
    {
        var (evaluation, membership) = await SeedWithMember();
        var actor = await Actor().OnEvaluation(evaluation.Id, permissions: [EvaluationPermission.ViewEvaluation]).SeedAsync();

        var memberships = await ReadAsync<List<EvaluationMembership>>(await Client(actor).GetAsync($"api/evaluations/{evaluation.Id}/memberships", Ct));

        Assert.Contains(memberships, x => x.Id == membership.Id && x.RoleId == TestData.EvaluationRoles.Member);
    }

    [Fact]
    public async Task GetAll_is_forbidden_for_a_caller_holding_only_ManageEvaluation()
    {
        var (evaluation, _) = await SeedWithMember();
        var actor = await Actor().OnEvaluation(evaluation.Id, permissions: [EvaluationPermission.ManageEvaluation]).SeedAsync();

        await AssertApiError(HttpStatusCode.Forbidden, await Client(actor).GetAsync($"api/evaluations/{evaluation.Id}/memberships", Ct));
    }

    [Fact]
    public async Task GetAll_is_forbidden_for_a_caller_holding_ViewEvaluation_only_on_another_evaluation()
    {
        var (evaluation, _) = await SeedWithMember();
        var actor = await Actor().OnNewEvaluation(EvaluationPermission.ViewEvaluation).SeedAsync();

        await AssertStatus(HttpStatusCode.Forbidden, await Client(actor).GetAsync($"api/evaluations/{evaluation.Id}/memberships", Ct));
    }

    [Fact]
    public async Task GetAll_lists_the_memberships_to_a_caller_holding_ViewEvaluations()
    {
        var (evaluation, membership) = await SeedWithMember();
        var actor = await Actor().WithSystemPermissions(SystemPermission.ViewEvaluations).SeedAsync();

        var memberships = await ReadAsync<List<EvaluationMembership>>(await Client(actor).GetAsync($"api/evaluations/{evaluation.Id}/memberships", Ct));

        Assert.Equal(membership.Id, Assert.Single(memberships).Id);
    }

    [Fact]
    public async Task GetAll_is_forbidden_for_a_caller_holding_only_ObserveEvaluations()
    {
        var (evaluation, _) = await SeedWithMember();
        var actor = await Actor().WithSystemPermissions(SystemPermission.ObserveEvaluations).SeedAsync();

        await AssertApiError(HttpStatusCode.Forbidden, await Client(actor).GetAsync($"api/evaluations/{evaluation.Id}/memberships", Ct));
    }

    [Fact]
    public async Task Get_returns_the_membership_to_a_caller_holding_ViewEvaluations()
    {
        var (_, membership) = await SeedWithMember();
        var actor = await Actor().WithSystemPermissions(SystemPermission.ViewEvaluations).SeedAsync();

        var read = await ReadAsync<EvaluationMembership>(await Client(actor).GetAsync($"api/evaluations/memberships/{membership.Id}", Ct));

        Assert.Equal(membership.UserId, read.UserId);
    }

    [Fact]
    public async Task Get_is_forbidden_for_a_caller_holding_only_ObserveEvaluations()
    {
        var (_, membership) = await SeedWithMember();
        var actor = await Actor().WithSystemPermissions(SystemPermission.ObserveEvaluations).SeedAsync();

        await AssertApiError(HttpStatusCode.Forbidden, await Client(actor).GetAsync($"api/evaluations/memberships/{membership.Id}", Ct));
    }

    [Fact]
    public async Task Get_returns_the_membership_to_a_caller_holding_ViewEvaluation_on_its_evaluation()
    {
        var (evaluation, membership) = await SeedWithMember();
        var actor = await Actor().OnEvaluation(evaluation.Id, permissions: [EvaluationPermission.ViewEvaluation]).SeedAsync();

        var read = await ReadAsync<EvaluationMembership>(await Client(actor).GetAsync($"api/evaluations/memberships/{membership.Id}", Ct));

        Assert.Equal(membership.UserId, read.UserId);
    }

    [Fact]
    public async Task Get_is_forbidden_for_a_caller_holding_ViewEvaluation_only_on_another_evaluation()
    {
        var (_, membership) = await SeedWithMember();
        var actor = await Actor().OnNewEvaluation(EvaluationPermission.ViewEvaluation).SeedAsync();

        await AssertStatus(HttpStatusCode.Forbidden, await Client(actor).GetAsync($"api/evaluations/memberships/{membership.Id}", Ct));
    }

    [Fact]
    public async Task Get_answers_an_unknown_membership_with_not_found()
    {
        await AssertApiError(HttpStatusCode.NotFound, await RootClient.GetAsync($"api/evaluations/memberships/{Guid.NewGuid()}", Ct));
    }

    [Fact]
    public async Task CreateMembership_adds_the_user_with_a_role_granting_ManageEvaluation_for_a_caller_holding_ManageEvaluation_on_it()
    {
        var evaluation = await SeedEvaluation();
        var user = TestData.User();
        var managerRole = TestData.EvaluationRole(EvaluationPermission.ManageEvaluation);
        await Seed(user, managerRole);
        var actor = await Actor().OnEvaluation(evaluation.Id, permissions: [EvaluationPermission.ManageEvaluation]).SeedAsync();

        var response = await Client(actor).PostAsJsonAsync($"api/evaluations/{evaluation.Id}/memberships",
            new { evaluationId = evaluation.Id, userId = user.Id, roleId = managerRole.Id }, Ct);

        await AssertStatus(HttpStatusCode.Created, response);
        await using var context = NewContext();
        Assert.Equal(managerRole.Id, (await context.EvaluationMemberships.SingleAsync(x => x.UserId == user.Id, Ct)).RoleId);
    }

    /// <summary>The membership is authorized by its stored evaluation, so an evaluation id in the body grants nothing.</summary>
    [Fact]
    public async Task Update_refuses_a_Viewer_raising_their_own_role_by_naming_an_owned_evaluation_in_the_body()
    {
        var owned = await TestScenario.SeedEvaluationAsync(Db, Ct, "Owned");
        var viewed = await TestScenario.SeedEvaluationAsync(Db, Ct, "Viewed");
        var actor = await Actor()
            .OnEvaluation(owned.Evaluation.Id, roleId: TestData.EvaluationRoles.Owner)
            .OnEvaluation(viewed.Evaluation.Id, roleId: TestData.EvaluationRoles.Viewer)
            .SeedAsync();
        var ownedMembership = await ReadBack(db => db.EvaluationMemberships.SingleAsync(x => x.EvaluationId == owned.Evaluation.Id && x.UserId == actor.Id, Ct));
        var viewedMembership = await ReadBack(db => db.EvaluationMemberships.SingleAsync(x => x.EvaluationId == viewed.Evaluation.Id && x.UserId == actor.Id, Ct));

        var response = await Client(actor).PutAsJsonAsync($"api/evaluations/memberships/{viewedMembership.Id}",
            new { id = viewedMembership.Id, evaluationId = ownedMembership.Id, userId = actor.Id, roleId = TestData.EvaluationRoles.Owner }, Ct);

        await AssertApiError(HttpStatusCode.Forbidden, response);
        var stored = await ReadBack(db => db.EvaluationMemberships.SingleAsync(x => x.Id == viewedMembership.Id, Ct));
        Assert.Equal((viewed.Evaluation.Id, TestData.EvaluationRoles.Viewer), (stored.EvaluationId, stored.RoleId));
    }

    // Same case as CreateMembership_lets_a_caller_holding_only_ManageEvaluation_grant_the_Owner_role.
    [Fact]
    public async Task Update_grants_the_evaluation_Owner_role_to_a_caller_holding_only_ManageEvaluation_when_the_membership_id_matches_the_evaluation()
    {
        var graph = await TestScenario.SeedEvaluationAsync(Db, Ct);
        var actor = await Actor().OnEvaluation(graph.Evaluation.Id, permissions: [EvaluationPermission.ManageEvaluation]).SeedAsync();
        var membership = TestData.EvaluationMembership(graph.Evaluation.Id, actor.Id, TestData.EvaluationRoles.Member);
        membership.Id = graph.Evaluation.Id;
        await Seed(membership);

        var response = await Client(actor).PutAsJsonAsync($"api/evaluations/memberships/{membership.Id}", new { id = membership.Id, evaluationId = graph.Evaluation.Id, userId = actor.Id, roleId = TestData.EvaluationRoles.Owner }, Ct);

        await AssertStatus(HttpStatusCode.OK, response);
        Assert.Equal(TestData.EvaluationRoles.Owner, await ReadBack(db => db.EvaluationMemberships.Where(x => x.Id == membership.Id).Select(x => x.RoleId).SingleAsync(Ct)));
    }

    /// <summary>A caller holding only ManageEvaluation gives another user the Owner role, which holds every evaluation permission.</summary>
    [Fact]
    public async Task CreateMembership_lets_a_caller_holding_only_ManageEvaluation_grant_the_Owner_role()
    {
        var evaluation = await SeedEvaluation();
        var user = TestData.User();
        await Seed(user);
        var actor = await Actor().OnEvaluation(evaluation.Id, permissions: [EvaluationPermission.ManageEvaluation]).SeedAsync();

        var response = await Client(actor).PostAsJsonAsync($"api/evaluations/{evaluation.Id}/memberships",
            new { evaluationId = evaluation.Id, userId = user.Id, roleId = TestData.EvaluationRoles.Owner }, Ct);

        await AssertStatus(HttpStatusCode.Created, response);
        await using var context = NewContext();
        Assert.Equal(TestData.EvaluationRoles.Owner, (await context.EvaluationMemberships.SingleAsync(x => x.UserId == user.Id, Ct)).RoleId);
    }

    [Fact]
    public async Task CreateMembership_is_forbidden_for_a_caller_holding_only_EditEvaluation()
    {
        var evaluation = await SeedEvaluation();
        var user = TestData.User();
        await Seed(user);
        var actor = await Actor().OnEvaluation(evaluation.Id, permissions: [EvaluationPermission.EditEvaluation]).SeedAsync();

        var response = await Client(actor).PostAsJsonAsync($"api/evaluations/{evaluation.Id}/memberships",
            new { evaluationId = evaluation.Id, userId = user.Id, roleId = TestData.EvaluationRoles.Owner }, Ct);

        await AssertStatus(HttpStatusCode.Forbidden, response);
        await using var context = NewContext();
        Assert.False(await context.EvaluationMemberships.AnyAsync(x => x.UserId == user.Id, Ct));
    }

    [Fact]
    public async Task CreateMembership_is_forbidden_for_a_caller_holding_ManageEvaluation_only_on_another_evaluation()
    {
        var evaluation = await SeedEvaluation();
        var user = TestData.User();
        await Seed(user);
        var actor = await Actor().OnNewEvaluation(EvaluationPermission.ManageEvaluation).SeedAsync();

        await AssertStatus(HttpStatusCode.Forbidden, await Client(actor).PostAsJsonAsync($"api/evaluations/{evaluation.Id}/memberships",
            new { evaluationId = evaluation.Id, userId = user.Id, roleId = TestData.EvaluationRoles.Owner }, Ct));
    }

    [Fact]
    public async Task CreateMembership_adds_the_user_for_a_caller_holding_ManageEvaluations()
    {
        var evaluation = await SeedEvaluation();
        var user = TestData.User();
        await Seed(user);
        var actor = await Actor().WithSystemPermissions(SystemPermission.ManageEvaluations).SeedAsync();

        var response = await Client(actor).PostAsJsonAsync($"api/evaluations/{evaluation.Id}/memberships",
            new { evaluationId = evaluation.Id, userId = user.Id, roleId = TestData.EvaluationRoles.Member }, Ct);

        await AssertStatus(HttpStatusCode.Created, response);
        await using var context = NewContext();
        Assert.Equal(TestData.EvaluationRoles.Member, (await context.EvaluationMemberships.SingleAsync(x => x.UserId == user.Id, Ct)).RoleId);
    }

    [Fact]
    public async Task CreateMembership_is_forbidden_for_a_caller_holding_only_EditEvaluations()
    {
        var evaluation = await SeedEvaluation();
        var user = TestData.User();
        await Seed(user);
        var actor = await Actor().WithSystemPermissions(SystemPermission.EditEvaluations).SeedAsync();

        var response = await Client(actor).PostAsJsonAsync($"api/evaluations/{evaluation.Id}/memberships",
            new { evaluationId = evaluation.Id, userId = user.Id, roleId = TestData.EvaluationRoles.Member }, Ct);

        await AssertApiError(HttpStatusCode.Forbidden, response);
        await using var context = NewContext();
        Assert.False(await context.EvaluationMemberships.AnyAsync(x => x.UserId == user.Id, Ct));
    }

    /// <summary>A second membership for a user already on the evaluation is stored beside the first.</summary>
    [Fact]
    public async Task CreateMembership_stores_a_second_membership_for_a_user_already_on_the_evaluation()
    {
        var (evaluation, membership) = await SeedWithMember();

        var response = await RootClient.PostAsJsonAsync($"api/evaluations/{evaluation.Id}/memberships",
            new { evaluationId = evaluation.Id, userId = membership.UserId, roleId = TestData.EvaluationRoles.Owner }, Ct);

        await AssertStatus(HttpStatusCode.Created, response);
        await using var context = NewContext();
        Assert.Equal(2, await context.EvaluationMemberships.CountAsync(x => x.EvaluationId == evaluation.Id && x.UserId == membership.UserId, Ct));
    }

    [Fact]
    public async Task CreateMembership_adds_the_user_to_the_evaluation_in_the_route_rather_than_the_body()
    {
        var routeEvaluation = await SeedEvaluation();
        var bodyEvaluation = await SeedEvaluation();
        var user = TestData.User();
        await Seed(user);

        var response = await RootClient.PostAsJsonAsync($"api/evaluations/{routeEvaluation.Id}/memberships",
            new { evaluationId = bodyEvaluation.Id, userId = user.Id, roleId = TestData.EvaluationRoles.Member }, Ct);

        await AssertStatus(HttpStatusCode.Created, response);
        await using var context = NewContext();
        Assert.Equal(routeEvaluation.Id, (await context.EvaluationMemberships.SingleAsync(x => x.UserId == user.Id, Ct)).EvaluationId);
    }

    [Fact]
    public async Task CreateMembership_is_forbidden_for_a_caller_holding_ManageEvaluation_only_on_the_evaluation_in_the_body()
    {
        var routeEvaluation = await SeedEvaluation();
        var user = TestData.User();
        await Seed(user);
        var actor = await Actor().OnNewEvaluation(EvaluationPermission.ManageEvaluation).SeedAsync();

        var response = await Client(actor).PostAsJsonAsync($"api/evaluations/{routeEvaluation.Id}/memberships",
            new { evaluationId = actor.NewEvaluationIds[0], userId = user.Id, roleId = TestData.EvaluationRoles.Owner }, Ct);

        await AssertApiError(HttpStatusCode.Forbidden, response);
        await using var context = NewContext();
        Assert.False(await context.EvaluationMemberships.AnyAsync(x => x.UserId == user.Id, Ct));
    }

    [Fact]
    public async Task Update_changes_the_role_for_a_caller_holding_ManageEvaluations()
    {
        var (evaluation, membership) = await SeedWithMember();
        var actor = await Actor().WithSystemPermissions(SystemPermission.ManageEvaluations).SeedAsync();

        var response = await Client(actor).PutAsJsonAsync($"api/evaluations/memberships/{membership.Id}",
            new { id = membership.Id, evaluationId = evaluation.Id, userId = membership.UserId, roleId = TestData.EvaluationRoles.Owner }, Ct);

        await AssertStatus(HttpStatusCode.OK, response);
        await using var context = NewContext();
        Assert.Equal(TestData.EvaluationRoles.Owner, (await context.EvaluationMemberships.SingleAsync(x => x.Id == membership.Id, Ct)).RoleId);
    }

    [Fact]
    public async Task Update_changes_the_role_for_a_caller_holding_ManageEvaluation_on_the_memberships_evaluation()
    {
        var (evaluation, membership) = await SeedWithMember();
        var actor = await Actor().OnEvaluation(evaluation.Id, permissions: [EvaluationPermission.ManageEvaluation]).SeedAsync();

        var response = await Client(actor).PutAsJsonAsync($"api/evaluations/memberships/{membership.Id}", new { roleId = TestData.EvaluationRoles.Owner }, Ct);

        await AssertStatus(HttpStatusCode.OK, response);
        await using var context = NewContext();
        Assert.Equal(TestData.EvaluationRoles.Owner, (await context.EvaluationMemberships.SingleAsync(x => x.Id == membership.Id, Ct)).RoleId);
    }

    /// <summary>The body names the caller's own membership in the evaluation they manage; the route names a membership elsewhere.</summary>
    [Fact]
    public async Task Update_is_forbidden_for_a_caller_holding_ManageEvaluation_only_on_another_evaluation_named_in_the_body()
    {
        var (_, membership) = await SeedWithMember();
        var actor = await Actor().OnNewEvaluation(EvaluationPermission.ManageEvaluation).SeedAsync();
        var ownMembershipId = await ReadBack(c => c.EvaluationMemberships.Where(x => x.UserId == actor.Id).Select(x => x.Id).SingleAsync(Ct));

        var response = await Client(actor).PutAsJsonAsync($"api/evaluations/memberships/{membership.Id}",
            new { id = membership.Id, evaluationId = ownMembershipId, roleId = TestData.EvaluationRoles.Owner }, Ct);

        await AssertApiError(HttpStatusCode.Forbidden, response);
        await using var context = NewContext();
        Assert.Equal(TestData.EvaluationRoles.Member, (await context.EvaluationMemberships.SingleAsync(x => x.Id == membership.Id, Ct)).RoleId);
    }

    [Fact]
    public async Task Update_answers_an_unknown_membership_with_not_found()
    {
        await AssertApiError(HttpStatusCode.NotFound, await RootClient.PutAsJsonAsync($"api/evaluations/memberships/{Guid.NewGuid()}", new { roleId = TestData.EvaluationRoles.Owner }, Ct));
    }

    [Fact]
    public async Task DeleteMembership_answers_an_unknown_membership_with_not_found()
    {
        await AssertApiError(HttpStatusCode.NotFound, await RootClient.DeleteAsync($"api/evaluations/memberships/{Guid.NewGuid()}", Ct));
    }

    /// <summary>A membership created with its evaluation's id is changed for a caller holding ManageEvaluation on that evaluation.</summary>
    [Fact]
    public async Task Update_changes_the_role_of_a_membership_whose_id_is_its_evaluations_id_for_a_caller_holding_ManageEvaluation_there()
    {
        var (evaluation, membership) = await SeedWithMemberIdOfItsEvaluation();
        var actor = await Actor().OnEvaluation(evaluation.Id, permissions: [EvaluationPermission.ManageEvaluation]).SeedAsync();

        var response = await Client(actor).PutAsJsonAsync($"api/evaluations/memberships/{membership.Id}",
            new { id = membership.Id, evaluationId = evaluation.Id, userId = membership.UserId, roleId = TestData.EvaluationRoles.Owner }, Ct);

        await AssertStatus(HttpStatusCode.OK, response);
        await using var context = NewContext();
        Assert.Equal(TestData.EvaluationRoles.Owner, (await context.EvaluationMemberships.SingleAsync(x => x.Id == membership.Id, Ct)).RoleId);
    }

    [Fact]
    public async Task Update_is_forbidden_on_a_membership_whose_id_is_its_evaluations_id_for_a_caller_holding_only_EditEvaluation_there()
    {
        var (evaluation, membership) = await SeedWithMemberIdOfItsEvaluation();
        var actor = await Actor().OnEvaluation(evaluation.Id, permissions: [EvaluationPermission.EditEvaluation]).SeedAsync();

        var response = await Client(actor).PutAsJsonAsync($"api/evaluations/memberships/{membership.Id}",
            new { id = membership.Id, evaluationId = evaluation.Id, userId = membership.UserId, roleId = TestData.EvaluationRoles.Owner }, Ct);

        await AssertApiError(HttpStatusCode.Forbidden, response);
        await using var context = NewContext();
        Assert.Equal(TestData.EvaluationRoles.Member, (await context.EvaluationMemberships.SingleAsync(x => x.Id == membership.Id, Ct)).RoleId);
    }

    [Fact]
    public async Task Update_is_forbidden_for_a_caller_holding_only_ManageScoringModels()
    {
        var (evaluation, membership) = await SeedWithMember();
        var actor = await Actor().WithSystemPermissions(SystemPermission.ManageScoringModels).SeedAsync();

        var response = await Client(actor).PutAsJsonAsync($"api/evaluations/memberships/{membership.Id}",
            new { id = membership.Id, evaluationId = evaluation.Id, userId = membership.UserId, roleId = TestData.EvaluationRoles.Owner }, Ct);

        await AssertStatus(HttpStatusCode.Forbidden, response);
        await using var context = NewContext();
        Assert.Equal(TestData.EvaluationRoles.Member, (await context.EvaluationMemberships.SingleAsync(x => x.Id == membership.Id, Ct)).RoleId);
    }

    [Fact]
    public async Task DeleteMembership_removes_the_membership_for_a_caller_holding_ManageEvaluation_on_its_evaluation()
    {
        var (evaluation, membership) = await SeedWithMember();
        var actor = await Actor().OnEvaluation(evaluation.Id, permissions: [EvaluationPermission.ManageEvaluation]).SeedAsync();

        await AssertStatus(HttpStatusCode.NoContent, await Client(actor).DeleteAsync($"api/evaluations/memberships/{membership.Id}", Ct));

        await using var context = NewContext();
        Assert.False(await context.EvaluationMemberships.AnyAsync(x => x.Id == membership.Id, Ct));
    }

    [Fact]
    public async Task DeleteMembership_is_forbidden_for_a_caller_holding_only_ViewEvaluation()
    {
        var (evaluation, membership) = await SeedWithMember();
        var actor = await Actor().OnEvaluation(evaluation.Id, permissions: [EvaluationPermission.ViewEvaluation]).SeedAsync();

        await AssertStatus(HttpStatusCode.Forbidden, await Client(actor).DeleteAsync($"api/evaluations/memberships/{membership.Id}", Ct));

        await using var context = NewContext();
        Assert.True(await context.EvaluationMemberships.AnyAsync(x => x.Id == membership.Id, Ct));
    }

    [Fact]
    public async Task DeleteMembership_removes_the_membership_for_a_caller_holding_ManageEvaluations()
    {
        var (_, membership) = await SeedWithMember();
        var actor = await Actor().WithSystemPermissions(SystemPermission.ManageEvaluations).SeedAsync();

        await AssertStatus(HttpStatusCode.NoContent, await Client(actor).DeleteAsync($"api/evaluations/memberships/{membership.Id}", Ct));

        await using var context = NewContext();
        Assert.False(await context.EvaluationMemberships.AnyAsync(x => x.Id == membership.Id, Ct));
    }

    [Fact]
    public async Task DeleteMembership_is_forbidden_for_a_caller_holding_only_EditEvaluations()
    {
        var (_, membership) = await SeedWithMember();
        var actor = await Actor().WithSystemPermissions(SystemPermission.EditEvaluations).SeedAsync();

        await AssertApiError(HttpStatusCode.Forbidden, await Client(actor).DeleteAsync($"api/evaluations/memberships/{membership.Id}", Ct));

        await using var context = NewContext();
        Assert.True(await context.EvaluationMemberships.AnyAsync(x => x.Id == membership.Id, Ct));
    }

    private async Task<EvaluationEntity> SeedEvaluation() => (await TestScenario.SeedEvaluationAsync(Db, Ct)).Evaluation;

    private async Task<(EvaluationEntity Evaluation, EvaluationMembershipEntity Membership)> SeedWithMember()
    {
        var evaluation = await SeedEvaluation();
        var user = TestData.User(name: "Member");
        var membership = TestData.EvaluationMembership(evaluation.Id, user.Id, TestData.EvaluationRoles.Member);
        await Seed(user, membership);

        return (evaluation, membership);
    }

    /// <summary>A member of a new evaluation whose membership row carries the evaluation's own id.</summary>
    private async Task<(EvaluationEntity Evaluation, EvaluationMembershipEntity Membership)> SeedWithMemberIdOfItsEvaluation()
    {
        var evaluation = await SeedEvaluation();
        var user = TestData.User(name: "Member");
        var membership = TestData.EvaluationMembership(evaluation.Id, user.Id, TestData.EvaluationRoles.Member);
        membership.Id = evaluation.Id;
        await Seed(user, membership);

        return (evaluation, membership);
    }
}
