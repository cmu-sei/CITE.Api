// Copyright 2026 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.

using Cite.Api.Data.Enumerations;
using Cite.Api.Infrastructure.Authorization;
using Cite.Api.Tests.Support;

namespace Cite.Api.Tests.Infrastructure.Authorization;

/// <summary>
/// The four requirement handlers <c>AuthorizationService</c> asks: any one listed permission is enough, and a
/// resource requirement only looks at the claim for its own resource.
/// </summary>
public class PermissionHandlerTests
{
    [Fact]
    public async Task SystemPermissionHandler_succeeds_on_any_one_of_the_required_permissions()
    {
        var user = new ClaimsPrincipalBuilder().WithSystemPermissions(SystemPermission.ObserveEvaluations).Build();

        var context = await AuthorizationHarness.HandleAsync(new SystemPermissionHandler(),
            new SystemPermissionRequirement([SystemPermission.ViewEvaluations, SystemPermission.ObserveEvaluations]), user);

        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public async Task SystemPermissionHandler_does_not_succeed_on_a_neighbouring_permission()
    {
        var user = new ClaimsPrincipalBuilder().WithSystemPermissions(SystemPermission.ViewEvaluations).Build();

        var context = await AuthorizationHarness.HandleAsync(new SystemPermissionHandler(),
            new SystemPermissionRequirement([SystemPermission.EditEvaluations]), user);

        Assert.False(context.HasSucceeded);
    }

    [Fact]
    public async Task SystemPermissionHandler_never_succeeds_on_an_empty_requirement()
    {
        var user = new ClaimsPrincipalBuilder().WithSystemPermissions(Enum.GetValues<SystemPermission>()).Build();

        var context = await AuthorizationHarness.HandleAsync(new SystemPermissionHandler(), new SystemPermissionRequirement([]), user);

        Assert.False(context.HasSucceeded);
    }

    [Fact]
    public async Task SystemPermissionHandler_ignores_a_permission_value_that_is_not_an_enum_name()
    {
        var user = new ClaimsPrincipalBuilder().WithRawSystemPermission("viewevaluations").Build();

        var context = await AuthorizationHarness.HandleAsync(new SystemPermissionHandler(),
            new SystemPermissionRequirement([SystemPermission.ViewEvaluations]), user);

        Assert.False(context.HasSucceeded);
    }

    [Fact]
    public async Task EvaluationPermissionHandler_succeeds_on_the_permission_in_that_evaluations_claim()
    {
        var evaluationId = Guid.NewGuid();
        var user = new ClaimsPrincipalBuilder().WithEvaluation(evaluationId, EvaluationPermission.EditEvaluation).Build();

        var context = await AuthorizationHarness.HandleAsync(new EvaluationPermissionHandler(),
            new EvaluationPermissionRequirement([EvaluationPermission.EditEvaluation], evaluationId), user);

        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public async Task EvaluationPermissionHandler_fails_on_the_permission_held_in_another_evaluations_claim()
    {
        var user = new ClaimsPrincipalBuilder().WithEvaluation(Guid.NewGuid(), EvaluationPermission.EditEvaluation).Build();

        var context = await AuthorizationHarness.HandleAsync(new EvaluationPermissionHandler(),
            new EvaluationPermissionRequirement([EvaluationPermission.EditEvaluation], Guid.NewGuid()), user);

        Assert.True(context.HasFailed);
    }

    /// <summary>An empty list of required permissions is met by any membership on the evaluation, even one granting nothing.</summary>
    [Fact]
    public async Task EvaluationPermissionHandler_succeeds_on_an_empty_requirement_for_any_member()
    {
        var evaluationId = Guid.NewGuid();
        var user = new ClaimsPrincipalBuilder().WithEvaluation(evaluationId).Build();

        var context = await AuthorizationHarness.HandleAsync(new EvaluationPermissionHandler(),
            new EvaluationPermissionRequirement([], evaluationId), user);

        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public async Task ScoringModelPermissionHandler_does_not_succeed_on_a_neighbouring_permission()
    {
        var scoringModelId = Guid.NewGuid();
        var user = new ClaimsPrincipalBuilder().WithScoringModel(scoringModelId, ScoringModelPermission.ViewScoringModel).Build();

        var context = await AuthorizationHarness.HandleAsync(new ScoringModelPermissionHandler(),
            new ScoringModelPermissionRequirement([ScoringModelPermission.EditScoringModel], scoringModelId), user);

        Assert.False(context.HasSucceeded);
    }

    [Fact]
    public async Task TeamPermissionHandler_succeeds_on_the_permission_in_that_teams_claim()
    {
        var teamId = Guid.NewGuid();
        var user = new ClaimsPrincipalBuilder().WithTeam(teamId, TeamPermission.SubmitTeamScore).Build();

        var context = await AuthorizationHarness.HandleAsync(new TeamPermissionHandler(),
            new TeamPermissionRequirement([TeamPermission.SubmitTeamScore], teamId), user);

        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public async Task TeamPermissionHandler_fails_without_a_claim_for_that_team()
    {
        var user = new ClaimsPrincipalBuilder().WithTeam(Guid.NewGuid(), TeamPermission.ManageTeam).Build();

        var context = await AuthorizationHarness.HandleAsync(new TeamPermissionHandler(),
            new TeamPermissionRequirement([TeamPermission.ManageTeam], Guid.NewGuid()), user);

        Assert.True(context.HasFailed);
    }

    [Fact]
    public async Task The_framework_service_runs_the_registered_handlers()
    {
        var service = AuthorizationHarness.CreateFrameworkAuthorizationService();
        var user = new ClaimsPrincipalBuilder().WithSystemPermissions(SystemPermission.ManageGroups).Build();

        var result = await service.AuthorizeAsync(user, null, [new SystemPermissionRequirement([SystemPermission.ManageGroups])]);

        Assert.True(result.Succeeded);
    }
}
