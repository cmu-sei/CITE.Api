// Copyright 2026 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.

// cite.api: the claims come from the real UserClaimsService over the seeded rows, with caching and the
// identity provider's roles and groups off, as TestConfiguration has them.

using System.Security.Claims;
using Microsoft.Extensions.Caching.Memory;
using Cite.Api.Data.Enumerations;
using Cite.Api.Infrastructure.Authorization;
using Cite.Api.Infrastructure.Options;
using Cite.Api.Services;

namespace Cite.Api.Tests.Support;

/// <summary>Tests for <see cref="TestActorBuilder"/>: an actor that holds more than asked turns an authorization test into a formality.</summary>
public class TestActorTests(DatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    [Fact]
    public async Task WithAllSystemPermissions_grants_every_system_permission()
    {
        var actor = await Actor().WithAllSystemPermissions().SeedAsync();

        Assert.Equal(Enum.GetNames<SystemPermission>().Order(), Permissions(await ClaimsOf(actor)));
    }

    [Fact]
    public async Task WithSystemPermissions_grants_exactly_what_it_names()
    {
        var first = Enum.GetValues<SystemPermission>()[0];
        var actor = await Actor().WithSystemPermissions(first).SeedAsync();

        Assert.Equal([first.ToString()], Permissions(await ClaimsOf(actor)));
    }

    [Fact]
    public async Task An_actor_with_no_role_holds_nothing()
    {
        var actor = await Actor().SeedAsync();

        var claims = await ClaimsOf(actor);

        Assert.DoesNotContain(claims.Claims, x => x.Type is AuthorizationConstants.PermissionClaimType
            or AuthorizationConstants.EvaluationPermissionClaimType
            or AuthorizationConstants.ScoringModelPermissionClaimType
            or AuthorizationConstants.TeamPermissionClaimType);
    }

    [Fact]
    public void WithRole_after_WithSystemPermissions_throws()
    {
        var builder = Actor().WithSystemPermissions(Enum.GetValues<SystemPermission>()[0]);

        Assert.Throws<InvalidOperationException>(() => builder.WithRole(TestData.Roles.Administrator));
    }

    [Fact]
    public async Task OnEvaluation_with_permissions_grants_exactly_those_on_that_evaluation()
    {
        var evaluation = await SeedEvaluation();

        var actor = await Actor().OnEvaluation(evaluation.Id, permissions: [EvaluationPermission.EditEvaluation]).SeedAsync();

        var claim = Assert.Single(EvaluationClaims(await ClaimsOf(actor)));
        Assert.Equal(evaluation.Id, claim.EvaluationId);
        Assert.Equal([EvaluationPermission.EditEvaluation], claim.Permissions);
    }

    [Fact]
    public async Task OnEvaluation_with_the_seeded_owner_role_grants_every_evaluation_permission()
    {
        var evaluation = await SeedEvaluation();

        var actor = await Actor().OnEvaluation(evaluation.Id, roleId: TestData.EvaluationRoles.Owner).SeedAsync();

        var claim = Assert.Single(EvaluationClaims(await ClaimsOf(actor)));
        Assert.Equal(Enum.GetValues<EvaluationPermission>().Order(), claim.Permissions.Order());
    }

    [Fact]
    public void OnEvaluation_with_both_a_role_and_permissions_throws()
    {
        var builder = Actor();

        Assert.Throws<InvalidOperationException>(() => builder.OnEvaluation(
            Guid.NewGuid(), TestData.EvaluationRoles.Owner, [EvaluationPermission.ViewEvaluation]));
    }

    [Fact]
    public void OnScoringModel_with_neither_a_role_nor_permissions_throws()
    {
        var builder = Actor();

        Assert.Throws<InvalidOperationException>(() => builder.OnScoringModel(Guid.NewGuid()));
    }

    [Fact]
    public async Task OnScoringModel_with_permissions_grants_exactly_those_on_that_scoring_model()
    {
        var scoringModel = TestData.ScoringModel();
        await Seed(scoringModel);

        var actor = await Actor().OnScoringModel(scoringModel.Id, permissions: [ScoringModelPermission.ViewScoringModel]).SeedAsync();

        var claim = Assert.Single(ScoringModelClaims(await ClaimsOf(actor)));
        Assert.Equal(scoringModel.Id, claim.ScoringModelId);
        Assert.Equal([ScoringModelPermission.ViewScoringModel], claim.Permissions);
    }

    [Fact]
    public async Task OnTeam_with_permissions_grants_exactly_those_on_that_team()
    {
        var team = await SeedTeam();

        var actor = await Actor().OnTeam(team.Id, permissions: [TeamPermission.SubmitTeamScore]).SeedAsync();

        var claim = Assert.Single(TeamClaims(await ClaimsOf(actor)));
        Assert.Equal(team.Id, claim.TeamId);
        Assert.Equal([TeamPermission.SubmitTeamScore], claim.Permissions);
    }

    /// <summary>A membership with no role is still a team claim, with no permissions in it.</summary>
    [Fact]
    public async Task OnTeam_with_no_role_grants_an_empty_claim_on_that_team()
    {
        var team = await SeedTeam();

        var actor = await Actor().OnTeam(team.Id).SeedAsync();

        var claim = Assert.Single(TeamClaims(await ClaimsOf(actor)));
        Assert.Equal(team.Id, claim.TeamId);
        Assert.Empty(claim.Permissions);
    }

    [Fact]
    public async Task InGroup_grants_the_groups_evaluation_membership_and_nothing_else()
    {
        var evaluation = await SeedEvaluation();
        var group = TestData.Group();
        var role = TestData.EvaluationRole(EvaluationPermission.ObserveEvaluation);
        await Seed(group, role, TestData.EvaluationMembership(evaluation.Id, null, role.Id, group.Id));

        var actor = await Actor().InGroup(group.Id).SeedAsync();

        var claims = await ClaimsOf(actor);
        var claim = Assert.Single(EvaluationClaims(claims));
        Assert.Equal(evaluation.Id, claim.EvaluationId);
        Assert.Equal([EvaluationPermission.ObserveEvaluation], claim.Permissions);
        Assert.Empty(Permissions(claims));
    }

    [Fact]
    public async Task InGroup_grants_the_groups_scoring_model_membership()
    {
        var scoringModel = TestData.ScoringModel();
        var group = TestData.Group();
        var role = TestData.ScoringModelRole(ScoringModelPermission.ViewScoringModel);
        await Seed(scoringModel, group, role, TestData.ScoringModelMembership(scoringModel.Id, null, role.Id, group.Id));

        var actor = await Actor().InGroup(group.Id).SeedAsync();

        var claim = Assert.Single(ScoringModelClaims(await ClaimsOf(actor)));
        Assert.Equal(scoringModel.Id, claim.ScoringModelId);
        Assert.Equal([ScoringModelPermission.ViewScoringModel], claim.Permissions);
    }

    [Fact]
    public async Task OnNewEvaluation_grants_exactly_what_it_names_on_a_new_evaluation()
    {
        var actor = await Actor().OnNewEvaluation(EvaluationPermission.ViewEvaluation).SeedAsync();

        var claim = Assert.Single(EvaluationClaims(await ClaimsOf(actor)));
        Assert.Equal(Assert.Single(actor.NewEvaluationIds), claim.EvaluationId);
        Assert.Equal([EvaluationPermission.ViewEvaluation], claim.Permissions);
    }

    [Fact]
    public async Task OnNewTeam_grants_exactly_what_it_names_on_a_new_team()
    {
        var evaluation = await SeedEvaluation();

        var actor = await Actor().OnNewTeam(evaluation.Id, TeamPermission.ViewTeam).SeedAsync();

        var claim = Assert.Single(TeamClaims(await ClaimsOf(actor)));
        Assert.Equal(Assert.Single(actor.NewTeamIds), claim.TeamId);
        Assert.Equal([TeamPermission.ViewTeam], claim.Permissions);
    }

    [Fact]
    public async Task OnNewScoringModel_grants_exactly_what_it_names_on_a_new_scoring_model()
    {
        var actor = await Actor().OnNewScoringModel(ScoringModelPermission.EditScoringModel).SeedAsync();

        var claim = Assert.Single(ScoringModelClaims(await ClaimsOf(actor)));
        Assert.Equal(Assert.Single(actor.NewScoringModelIds), claim.ScoringModelId);
        Assert.Equal([ScoringModelPermission.EditScoringModel], claim.Permissions);
    }

    private TestActorBuilder Actor() => new(Db, Ct);

    private async Task<Cite.Api.Data.Models.EvaluationEntity> SeedEvaluation()
    {
        var scoringModel = TestData.ScoringModel();
        var evaluation = TestData.Evaluation(scoringModel.Id);
        await Seed(scoringModel, evaluation);

        return evaluation;
    }

    private async Task<Cite.Api.Data.Models.TeamEntity> SeedTeam()
    {
        var evaluation = await SeedEvaluation();
        var teamType = TestData.TeamType();
        var team = TestData.Team(evaluation.Id, teamType.Id);
        await Seed(teamType, team);

        return team;
    }

    private async Task<ClaimsPrincipal> ClaimsOf(TestActor actor)
    {
        await using var context = NewContext();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new UserClaimsService(context, cache, new ClaimsTransformationOptions());
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", actor.Id.ToString())], "Test"));

        return await service.AddUserClaims(principal, update: false);
    }

    private static string[] Permissions(ClaimsPrincipal principal) =>
        [.. principal.Claims
            .Where(x => x.Type == AuthorizationConstants.PermissionClaimType)
            .Select(x => x.Value)
            .Order()];

    private static EvaluationPermissionClaim[] EvaluationClaims(ClaimsPrincipal principal) =>
        [.. principal.Claims
            .Where(x => x.Type == AuthorizationConstants.EvaluationPermissionClaimType)
            .Select(x => EvaluationPermissionClaim.FromString(x.Value))];

    private static ScoringModelPermissionClaim[] ScoringModelClaims(ClaimsPrincipal principal) =>
        [.. principal.Claims
            .Where(x => x.Type == AuthorizationConstants.ScoringModelPermissionClaimType)
            .Select(x => ScoringModelPermissionClaim.FromString(x.Value))];

    private static TeamPermissionClaim[] TeamClaims(ClaimsPrincipal principal) =>
        [.. principal.Claims
            .Where(x => x.Type == AuthorizationConstants.TeamPermissionClaimType)
            .Select(x => TeamPermissionClaim.FromString(x.Value))];
}
