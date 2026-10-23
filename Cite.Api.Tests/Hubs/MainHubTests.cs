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
    public async Task SwitchTeam_joins_the_team_its_evaluation_and_its_scoring_model_for_a_member_holding_ViewTeam()
    {
        var graph = await TestScenario.SeedEvaluationAsync(Db, Ct);
        var harness = await MemberOf(graph.Team.Id, TeamPermission.ViewTeam);

        await Hub(harness).SwitchTeam([graph.Team.Id, graph.Team.Id]);

        Assert.Equal(
            [harness.UserId.ToString(), graph.Team.Id.ToString(), graph.Evaluation.Id.ToString(), graph.ScoringModel.Id.ToString()],
            Joined(harness));
    }

    [Fact]
    public async Task SwitchTeam_joins_only_the_callers_own_group_for_a_member_without_ViewTeam()
    {
        var graph = await TestScenario.SeedEvaluationAsync(Db, Ct, officialScoreContributor: true);
        var harness = await MemberOf(graph.Team.Id, TeamPermission.EditTeamScore);

        await Hub(harness).SwitchTeam([graph.Team.Id, graph.Team.Id]);

        Assert.Equal([harness.UserId.ToString()], Joined(harness));
    }

    [Theory]
    [InlineData(TeamPermission.ViewPastOfficialScore, true, false)]
    [InlineData(TeamPermission.ViewCurrentOfficialScore, false, true)]
    [InlineData(TeamPermission.EditOfficialScore, false, true)]
    public async Task SwitchTeam_joins_the_official_score_groups_the_members_team_permission_reads(TeamPermission permission, bool past, bool current)
    {
        var graph = await TestScenario.SeedEvaluationAsync(Db, Ct);
        var harness = await MemberOf(graph.Team.Id, TeamPermission.ViewTeam, permission);

        await Hub(harness).SwitchTeam([graph.Team.Id, graph.Team.Id]);

        var joined = Joined(harness);
        Assert.Equal((past, current), (joined.Contains(graph.Evaluation.Id.ToString() + MainHub.PAST_OFFICIAL_SCORE_POSTFIX), joined.Contains(graph.Evaluation.Id.ToString() + MainHub.OFFICIAL_SCORE_POSTFIX)));
    }

    /// <summary>The contributor flag on the team type no longer grants the official score.</summary>
    [Fact]
    public async Task SwitchTeam_leaves_a_member_of_an_official_score_contributor_out_of_the_official_score_groups_without_a_permission()
    {
        var graph = await TestScenario.SeedEvaluationAsync(Db, Ct, officialScoreContributor: true);
        var harness = await MemberOf(graph.Team.Id, TeamPermission.ViewTeam);

        await Hub(harness).SwitchTeam([graph.Team.Id, graph.Team.Id]);

        Assert.DoesNotContain(graph.Evaluation.Id.ToString() + MainHub.OFFICIAL_SCORE_POSTFIX, Joined(harness));
    }

    [Fact]
    public async Task SwitchTeam_joins_only_the_callers_own_group_for_a_member_of_a_sibling_team()
    {
        var graph = await TestScenario.SeedEvaluationAsync(Db, Ct);
        var sibling = TestData.Team(graph.Evaluation.Id, graph.TeamType.Id, "Sibling");
        await Seed(sibling);
        var harness = await MemberOf(sibling.Id, TeamPermission.ViewTeam);

        await Hub(harness).SwitchTeam([graph.Team.Id, graph.Team.Id]);

        Assert.Equal([harness.UserId.ToString()], Joined(harness));
    }

    [Fact]
    public async Task SwitchTeam_joins_another_team_and_its_official_scores_for_a_caller_holding_ObserveEvaluation_on_the_evaluation()
    {
        var graph = await TestScenario.SeedEvaluationAsync(Db, Ct);
        var principal = new ClaimsPrincipalBuilder().WithEvaluation(graph.Evaluation.Id, EvaluationPermission.ObserveEvaluation);
        var harness = new HubHarness(principal.UserId, principal.Build());

        await Hub(harness).SwitchTeam([graph.Team.Id, graph.Team.Id]);

        Assert.Equal(
            [harness.UserId.ToString(), graph.Evaluation.Id.ToString() + MainHub.ALL_MOVES_POSTFIX,
                graph.Team.Id.ToString(), graph.Evaluation.Id.ToString(), graph.ScoringModel.Id.ToString(),
                graph.Evaluation.Id.ToString() + MainHub.PAST_OFFICIAL_SCORE_POSTFIX, graph.Evaluation.Id.ToString() + MainHub.OFFICIAL_SCORE_POSTFIX],
            Joined(harness));
    }

    /// <summary>The all-moves group carries moves ahead of the current one, read by the same permissions as MoveController.</summary>
    [Theory]
    [InlineData(EvaluationPermission.ViewEvaluation)]
    [InlineData(EvaluationPermission.ObserveEvaluation)]
    [InlineData(EvaluationPermission.ExecuteEvaluation)]
    public async Task SwitchTeam_joins_the_all_moves_group_for_a_caller_holding_an_evaluation_permission_that_reads_every_move(EvaluationPermission permission)
    {
        var graph = await TestScenario.SeedEvaluationAsync(Db, Ct);
        var principal = new ClaimsPrincipalBuilder().WithEvaluation(graph.Evaluation.Id, permission);
        var harness = new HubHarness(principal.UserId, principal.Build());

        await Hub(harness).SwitchTeam([graph.Team.Id, graph.Team.Id]);

        Assert.Contains(graph.Evaluation.Id.ToString() + MainHub.ALL_MOVES_POSTFIX, Joined(harness));
    }

    [Theory]
    [InlineData(SystemPermission.ViewEvaluations)]
    [InlineData(SystemPermission.ObserveEvaluations)]
    [InlineData(SystemPermission.ExecuteEvaluations)]
    public async Task SwitchTeam_joins_the_all_moves_group_for_a_caller_holding_a_system_permission_that_reads_every_move(SystemPermission permission)
    {
        var graph = await TestScenario.SeedEvaluationAsync(Db, Ct);
        var principal = new ClaimsPrincipalBuilder().WithSystemPermissions(permission);
        var harness = new HubHarness(principal.UserId, principal.Build());

        await Hub(harness).SwitchTeam([graph.Team.Id, graph.Team.Id]);

        Assert.Contains(graph.Evaluation.Id.ToString() + MainHub.ALL_MOVES_POSTFIX, Joined(harness));
    }

    [Fact]
    public async Task SwitchTeam_leaves_a_caller_holding_only_EditEvaluations_out_of_the_all_moves_group()
    {
        var graph = await TestScenario.SeedEvaluationAsync(Db, Ct);
        var principal = new ClaimsPrincipalBuilder().WithSystemPermissions(SystemPermission.EditEvaluations);
        var harness = new HubHarness(principal.UserId, principal.Build());

        await Hub(harness).SwitchTeam([graph.Team.Id, graph.Team.Id]);

        Assert.DoesNotContain(graph.Evaluation.Id.ToString() + MainHub.ALL_MOVES_POSTFIX, Joined(harness));
    }

    [Fact]
    public async Task SwitchTeam_leaves_a_caller_holding_EditEvaluation_out_of_the_all_moves_group()
    {
        var graph = await TestScenario.SeedEvaluationAsync(Db, Ct);
        var principal = new ClaimsPrincipalBuilder().WithEvaluation(graph.Evaluation.Id, EvaluationPermission.EditEvaluation);
        var harness = new HubHarness(principal.UserId, principal.Build());

        await Hub(harness).SwitchTeam([graph.Team.Id, graph.Team.Id]);

        Assert.DoesNotContain(graph.Evaluation.Id.ToString() + MainHub.ALL_MOVES_POSTFIX, Joined(harness));
    }

    [Fact]
    public async Task SwitchTeam_joins_only_the_callers_own_group_for_a_caller_holding_ObserveEvaluation_only_on_another_evaluation()
    {
        var graph = await TestScenario.SeedEvaluationAsync(Db, Ct);
        var principal = new ClaimsPrincipalBuilder().WithEvaluation(Guid.NewGuid(), EvaluationPermission.ObserveEvaluation);
        var harness = new HubHarness(principal.UserId, principal.Build());

        await Hub(harness).SwitchTeam([graph.Team.Id, graph.Team.Id]);

        Assert.Equal([harness.UserId.ToString()], Joined(harness));
    }

    [Fact]
    public async Task SwitchTeam_joins_the_new_team_when_the_team_being_left_does_not_exist()
    {
        var graph = await TestScenario.SeedEvaluationAsync(Db, Ct);
        var harness = await MemberOf(graph.Team.Id, TeamPermission.ViewTeam);

        await Hub(harness).SwitchTeam([Guid.NewGuid(), graph.Team.Id]);

        Assert.Contains(graph.Team.Id.ToString(), Joined(harness));
    }

    /// <summary>Leaving removes every group of the old team, so a permission lost since the join does not keep one.</summary>
    [Fact]
    public async Task SwitchTeam_leaves_every_group_of_the_old_team_for_a_member_without_permissions()
    {
        var graph = await TestScenario.SeedEvaluationAsync(Db, Ct);
        var harness = await MemberOf(graph.Team.Id);

        await Hub(harness).SwitchTeam([graph.Team.Id, graph.Team.Id]);

        var left = harness.Groups.ReceivedCalls()
            .Where(x => x.GetMethodInfo().Name == nameof(IGroupManager.RemoveFromGroupAsync))
            .Select(x => (string)x.GetArguments()[1]);
        Assert.Contains(graph.Evaluation.Id.ToString() + MainHub.OFFICIAL_SCORE_POSTFIX, left);
        Assert.Contains(graph.Evaluation.Id.ToString() + MainHub.ALL_MOVES_POSTFIX, left);
        Assert.Contains(graph.Team.Id.ToString(), left);
    }

    [Fact]
    public async Task SwitchTeam_with_other_than_two_ids_joins_nothing()
    {
        var harness = new HubHarness();

        await Hub(harness).SwitchTeam([Guid.NewGuid()]);

        Assert.Empty(Joined(harness));
    }

    /// <summary>A member of the team whose principal carries the team claim the claims transformer builds from <paramref name="permissions"/>.</summary>
    private async Task<HubHarness> MemberOf(Guid teamId, params TeamPermission[] permissions)
    {
        var principal = new ClaimsPrincipalBuilder().WithTeam(teamId, permissions);
        var harness = new HubHarness(principal.UserId, principal.Build());
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
