// Copyright 2026 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.

using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Cite.Api.Data;
using Cite.Api.Data.Enumerations;
using Cite.Api.Hubs;
using Cite.Api.Infrastructure.Authorization;
using Cite.Api.Infrastructure.Identity;
using Cite.Api.Tests.Support;

namespace Cite.Api.Tests.Hubs;

/// <summary>
/// <see cref="MainHub"/>: which groups a connection joins. The hub reads the database through a scope of
/// its own and asks the real <see cref="AuthorizationService"/>, so both are wired over this test's
/// database; the caller is a principal shaped like the claims transformer's output.
/// </summary>
public class MainHubTests(DatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    private readonly List<ServiceProvider> _providers = [];

    public override async ValueTask DisposeAsync()
    {
        foreach (var provider in _providers)
        {
            await provider.DisposeAsync();
        }

        await base.DisposeAsync();
    }

    [Fact]
    public async Task Join_adds_the_connection_to_the_callers_own_group()
    {
        var harness = new HubHarness();

        await Hub(harness).Join();

        Assert.Equal([harness.UserId.ToString()], Joined(harness));
    }

    [Fact]
    public async Task Leave_removes_the_connection_from_the_callers_own_group()
    {
        var harness = new HubHarness();

        await Hub(harness).Leave();

        await harness.Groups.Received(1).RemoveFromGroupAsync(HubHarness.ConnectionId, harness.UserId.ToString(), Arg.Any<CancellationToken>());
    }

    /// <summary>A caller holding ViewEvaluations joins its own group and AdminDataGroup.</summary>
    [Fact]
    public async Task JoinAdmin_adds_a_caller_holding_ViewEvaluations_to_its_own_group_and_the_admin_data_group_only()
    {
        var principal = new ClaimsPrincipalBuilder().WithSystemPermissions(SystemPermission.ViewEvaluations);
        var harness = new HubHarness(principal.UserId, principal.Build());

        await Hub(harness).JoinAdmin();

        Assert.Equal([harness.UserId.ToString(), MainHub.ADMIN_DATA_GROUP], Joined(harness));
    }

    [Fact]
    public async Task JoinAdmin_leaves_a_caller_holding_only_ViewScoringModels_out_of_the_admin_data_group()
    {
        var principal = new ClaimsPrincipalBuilder().WithSystemPermissions(SystemPermission.ViewScoringModels);
        var harness = new HubHarness(principal.UserId, principal.Build());

        await Hub(harness).JoinAdmin();

        Assert.Equal([harness.UserId.ToString()], Joined(harness));
    }

    [Fact]
    public async Task SwitchTeam_joins_the_team_its_evaluation_and_its_scoring_model_for_a_member()
    {
        var graph = await TestScenario.SeedEvaluationAsync(Db, Ct);
        var harness = await MemberOf(graph.Team.Id);

        await Hub(harness).SwitchTeam([graph.Team.Id, graph.Team.Id]);

        Assert.Equal(
            [harness.UserId.ToString(), graph.Team.Id.ToString(), graph.Evaluation.Id.ToString(), graph.ScoringModel.Id.ToString()],
            Joined(harness));
    }

    [Fact]
    public async Task SwitchTeam_also_joins_the_official_score_group_for_a_member_of_an_official_score_contributor()
    {
        var graph = await TestScenario.SeedEvaluationAsync(Db, Ct, officialScoreContributor: true);
        var harness = await MemberOf(graph.Team.Id);

        await Hub(harness).SwitchTeam([graph.Team.Id, graph.Team.Id]);

        Assert.Contains(graph.Evaluation.Id + MainHub.OFFICIAL_SCORE_POSTFIX, Joined(harness));
    }

    [Fact]
    public async Task SwitchTeam_joins_only_the_callers_own_group_for_a_member_of_a_sibling_team()
    {
        var graph = await TestScenario.SeedEvaluationAsync(Db, Ct);
        var sibling = TestData.Team(graph.Evaluation.Id, graph.TeamType.Id, "Sibling");
        await Seed(sibling);
        var harness = await MemberOf(sibling.Id);

        await Hub(harness).SwitchTeam([graph.Team.Id, graph.Team.Id]);

        Assert.Equal([harness.UserId.ToString()], Joined(harness));
    }

    [Fact]
    public async Task SwitchTeam_joins_another_team_for_a_member_holding_ObserveEvaluation_on_the_evaluation()
    {
        var graph = await TestScenario.SeedEvaluationAsync(Db, Ct);
        var sibling = TestData.Team(graph.Evaluation.Id, graph.TeamType.Id, "Sibling");
        await Seed(sibling);
        var harness = await MemberOf(sibling.Id);
        var role = TestData.EvaluationRole(EvaluationPermission.ObserveEvaluation);
        await Seed(role, TestData.EvaluationMembership(graph.Evaluation.Id, harness.UserId, role.Id));

        await Hub(harness).SwitchTeam([graph.Team.Id, graph.Team.Id]);

        Assert.Contains(graph.Team.Id.ToString(), Joined(harness));
    }

    /// <summary>A previous team id that names no team throws before the null check that follows its lookup.</summary>
    [Fact]
    public async Task SwitchTeam_throws_when_the_team_being_left_does_not_exist()
    {
        var graph = await TestScenario.SeedEvaluationAsync(Db, Ct);
        var harness = await MemberOf(graph.Team.Id);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => Hub(harness).SwitchTeam([Guid.NewGuid(), graph.Team.Id]));

        Assert.IsType<NullReferenceException>(error.InnerException);
    }

    [Fact]
    public async Task SwitchTeam_with_other_than_two_ids_joins_nothing()
    {
        var harness = new HubHarness();

        await Hub(harness).SwitchTeam([Guid.NewGuid()]);

        Assert.Empty(Joined(harness));
    }

    private async Task<HubHarness> MemberOf(Guid teamId)
    {
        var harness = new HubHarness();
        await Seed(TestData.User(harness.UserId), TestData.TeamMembership(teamId, harness.UserId));

        return harness;
    }

    private MainHub Hub(HubHarness harness)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped(_ => NewContext());
        services.AddSingleton(AuthorizationHarness.CreateFrameworkAuthorizationService());
        services.AddSingleton<IHttpContextAccessor>(new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = harness.User } });
        services.AddScoped<IIdentityResolver, IdentityResolver>();
        services.AddScoped<ICiteAuthorizationService, AuthorizationService>();
        var provider = services.BuildServiceProvider();
        _providers.Add(provider);

        return harness.Attach(new MainHub(
            null,
            null,
            null,
            provider.GetRequiredService<IServiceScopeFactory>(),
            provider.GetRequiredService<ICiteAuthorizationService>()));
    }

    /// <summary>The groups the connection was added to, in order.</summary>
    private static string[] Joined(HubHarness harness) =>
        [.. harness.Groups.ReceivedCalls()
            .Where(x => x.GetMethodInfo().Name == nameof(IGroupManager.AddToGroupAsync))
            .Select(x => (string)x.GetArguments()[1])];
}
