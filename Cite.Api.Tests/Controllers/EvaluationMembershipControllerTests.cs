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

    /// <summary>The membership is created on the evaluation the body names, whatever evaluation the route names.</summary>
    [Fact]
    public async Task CreateMembership_adds_the_user_to_the_evaluation_in_the_body_rather_than_the_route()
    {
        var routeEvaluation = await SeedEvaluation();
        var bodyEvaluation = await SeedEvaluation();
        var user = TestData.User();
        await Seed(user);

        var response = await RootClient.PostAsJsonAsync($"api/evaluations/{routeEvaluation.Id}/memberships",
            new { evaluationId = bodyEvaluation.Id, userId = user.Id, roleId = TestData.EvaluationRoles.Member }, Ct);

        await AssertStatus(HttpStatusCode.Created, response);
        await using var context = NewContext();
        Assert.Equal(bodyEvaluation.Id, (await context.EvaluationMemberships.SingleAsync(x => x.UserId == user.Id, Ct)).EvaluationId);
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

    /// <summary>ManageEvaluation on the membership's evaluation does not let its holder change a role there.</summary>
    [Fact]
    public async Task Update_is_forbidden_for_a_caller_holding_ManageEvaluation_on_the_memberships_evaluation()
    {
        var (evaluation, membership) = await SeedWithMember();
        var actor = await Actor().OnEvaluation(evaluation.Id, permissions: [EvaluationPermission.ManageEvaluation]).SeedAsync();

        var response = await Client(actor).PutAsJsonAsync($"api/evaluations/memberships/{membership.Id}",
            new { id = membership.Id, evaluationId = evaluation.Id, userId = membership.UserId, roleId = TestData.EvaluationRoles.Owner }, Ct);

        await AssertStatus(HttpStatusCode.Forbidden, response);
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

    private async Task<EvaluationEntity> SeedEvaluation() => (await TestScenario.SeedEvaluationAsync(Db, Ct)).Evaluation;

    private async Task<(EvaluationEntity Evaluation, EvaluationMembershipEntity Membership)> SeedWithMember()
    {
        var evaluation = await SeedEvaluation();
        var user = TestData.User(name: "Member");
        var membership = TestData.EvaluationMembership(evaluation.Id, user.Id, TestData.EvaluationRoles.Member);
        await Seed(user, membership);

        return (evaluation, membership);
    }
}
