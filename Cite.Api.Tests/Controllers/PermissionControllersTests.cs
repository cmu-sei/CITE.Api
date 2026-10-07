// Copyright 2026 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.

using System.Net;
using Cite.Api.Data.Enumerations;
using Cite.Api.Infrastructure.Authorization;
using Cite.Api.Tests.Support;

namespace Cite.Api.Tests.Controllers;

/// <summary>
/// <c>SystemPermissionsController</c>, <c>EvaluationPermissionsController</c>,
/// <c>ScoringModelPermissionsController</c> and <c>TeamPermissionsController</c>: each answers the caller's
/// own permissions of one kind, as the real claims transformer derived them from the seeded rows. None
/// checks a permission.
/// </summary>
public class PermissionControllersTests(DatabaseFixture fixture, CiteAppFactory factory) : ApiTestBase(fixture, factory)
{
    /// <summary>A request with no identity is answered with a 401; the anonymous client is the case under test.</summary>
    [Fact]
    public async Task GetMySystemPermissions_without_an_identity_is_unauthorized()
    {
        await AssertStatus(HttpStatusCode.Unauthorized, await Client().GetAsync("api/me/systemPermissions", Ct));
    }

    [Fact]
    public async Task GetMySystemPermissions_answers_exactly_the_permissions_of_the_callers_role()
    {
        var actor = await Actor().WithSystemPermissions(SystemPermission.ViewUsers, SystemPermission.ManageGroups).SeedAsync();

        var permissions = await ReadAsync<SystemPermission[]>(await Client(actor).GetAsync("api/me/systemPermissions", Ct));

        Assert.Equal([SystemPermission.ViewUsers, SystemPermission.ManageGroups], permissions.Order());
    }

    [Fact]
    public async Task GetMySystemPermissions_answers_every_permission_for_the_administrator_role()
    {
        var permissions = await ReadAsync<SystemPermission[]>(await RootClient.GetAsync("api/me/systemPermissions", Ct));

        Assert.Equal(Enum.GetValues<SystemPermission>().Order(), permissions.Order());
    }

    [Fact]
    public async Task GetMyEvaluationPermissions_answers_one_claim_per_evaluation_membership()
    {
        var actor = await Actor().OnNewEvaluation(EvaluationPermission.ExecuteEvaluation).SeedAsync();

        var claims = await ReadAsync<EvaluationPermissionClaim[]>(await Client(actor).GetAsync("api/evaluation-permissions", Ct));

        var claim = Assert.Single(claims);
        Assert.Equal(actor.NewEvaluationIds[0], claim.EvaluationId);
        Assert.Equal([EvaluationPermission.ExecuteEvaluation], claim.Permissions);
    }

    [Fact]
    public async Task GetMyScoringModelPermissions_answers_one_claim_per_scoring_model_membership()
    {
        var actor = await Actor().OnNewScoringModel(ScoringModelPermission.EditScoringModel).SeedAsync();

        var claims = await ReadAsync<ScoringModelPermissionClaim[]>(await Client(actor).GetAsync("api/scoringModels-permissions", Ct));

        var claim = Assert.Single(claims);
        Assert.Equal(actor.NewScoringModelIds[0], claim.ScoringModelId);
        Assert.Equal([ScoringModelPermission.EditScoringModel], claim.Permissions);
    }

    [Fact]
    public async Task GetMyTeamPermissions_answers_one_claim_per_team_membership()
    {
        var actor = await Actor().OnNewTeam(null, TeamPermission.SubmitTeamScore).SeedAsync();

        var claims = await ReadAsync<TeamPermissionClaim[]>(await Client(actor).GetAsync("api/team-permissions", Ct));

        var claim = Assert.Single(claims);
        Assert.Equal(actor.NewTeamIds[0], claim.TeamId);
        Assert.Equal([TeamPermission.SubmitTeamScore], claim.Permissions);
    }

    /// <summary>Evaluation memberships held through a group reach the group's members.</summary>
    [Fact]
    public async Task GetMyEvaluationPermissions_includes_a_membership_held_by_the_callers_group()
    {
        var scoringModel = TestData.ScoringModel();
        var evaluation = TestData.Evaluation(scoringModel.Id);
        var group = TestData.Group();
        var role = TestData.EvaluationRole(EvaluationPermission.ObserveEvaluation);
        await Seed(scoringModel, evaluation, group, role, TestData.EvaluationMembership(evaluation.Id, null, role.Id, group.Id));
        var actor = await Actor().InGroup(group.Id).SeedAsync();

        var claims = await ReadAsync<EvaluationPermissionClaim[]>(await Client(actor).GetAsync("api/evaluation-permissions", Ct));

        var claim = Assert.Single(claims);
        Assert.Equal(evaluation.Id, claim.EvaluationId);
        Assert.Equal([EvaluationPermission.ObserveEvaluation], claim.Permissions);
    }
}
