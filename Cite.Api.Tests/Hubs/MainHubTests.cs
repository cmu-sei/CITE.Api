// Copyright 2026 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.

using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Cite.Api.Data;
using Cite.Api.Data.Enumerations;
using Cite.Api.Hubs;
using Cite.Api.Infrastructure.Authorization;
using Cite.Api.Infrastructure.Identity;
using Cite.Api.Infrastructure.Options;
using Cite.Api.Services;
using Cite.Api.Tests.Support;

namespace Cite.Api.Tests.Hubs;

/// <summary>
/// <see cref="MainHub"/>: which groups a connection joins. The hub reads the database through a scope of
/// its own and asks the real <see cref="AuthorizationService"/>, so both are wired over this test's
/// database; the caller is a principal shaped like the claims transformer's output. The claims the hub
/// rebuilds before joining are the principal's own, unless a test passes the real <see cref="UserClaimsService"/>.
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
            [harness.UserId.ToString(), graph.Team.Id.ToString(), graph.Evaluation.Id.ToString(), graph.ScoringModel.Id.ToString(),
                graph.Evaluation.Id.ToString() + MainHub.CURRENT_MOVES_POSTFIX],
            Joined(harness));
    }

    /// <summary>
    /// The connection opened before the user was put on the team, so its claims name no team; the hub reads the
    /// membership and its role from the database as they are now.
    /// </summary>
    [Fact]
    public async Task SwitchTeam_joins_the_team_for_a_member_added_after_the_connection_opened()
    {
        var graph = await TestScenario.SeedEvaluationAsync(Db, Ct);
        var principal = new ClaimsPrincipalBuilder();
        var harness = new HubHarness(principal.UserId, principal.Build());
        var role = TestData.TeamRole(TeamPermission.ViewTeam);
        await Seed(TestData.User(harness.UserId), role, TestData.TeamMembership(graph.Team.Id, harness.UserId, role.Id));

        await Hub(harness, claimsFromDatabase: true).SwitchTeam([graph.Team.Id, graph.Team.Id]);

        Assert.Contains(graph.Team.Id.ToString(), Joined(harness));
    }

    /// <summary>A permission removed since the connection opened no longer joins the team's groups.</summary>
    [Fact]
    public async Task SwitchTeam_leaves_out_a_team_whose_membership_was_removed_after_the_connection_opened()
    {
        var graph = await TestScenario.SeedEvaluationAsync(Db, Ct);
        var principal = new ClaimsPrincipalBuilder().WithTeam(graph.Team.Id, TeamPermission.ViewTeam);
        var harness = new HubHarness(principal.UserId, principal.Build());
        await Seed(TestData.User(harness.UserId));

        await Hub(harness, claimsFromDatabase: true).SwitchTeam([graph.Team.Id, graph.Team.Id]);

        Assert.Equal([harness.UserId.ToString()], Joined(harness));
    }

    /// <summary>Moves reach a caller through one group only: the all-moves group for one who reads every move.</summary>
    [Fact]
    public async Task SwitchTeam_leaves_a_caller_who_reads_every_move_out_of_the_current_moves_group()
    {
        var graph = await TestScenario.SeedEvaluationAsync(Db, Ct);
        var principal = new ClaimsPrincipalBuilder()
            .WithTeam(graph.Team.Id, TeamPermission.ViewTeam)
            .WithEvaluation(graph.Evaluation.Id, EvaluationPermission.ViewEvaluation);
        var harness = new HubHarness(principal.UserId, principal.Build());
        await Seed(TestData.User(harness.UserId), TestData.TeamMembership(graph.Team.Id, harness.UserId));

        await Hub(harness).SwitchTeam([graph.Team.Id, graph.Team.Id]);

        var joined = Joined(harness);
        Assert.Equal((true, false, true), (
            joined.Contains(graph.Evaluation.Id.ToString() + MainHub.ALL_MOVES_POSTFIX),
            joined.Contains(graph.Evaluation.Id.ToString() + MainHub.CURRENT_MOVES_POSTFIX),
            joined.Contains(graph.Team.Id.ToString())));
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
        Assert.Contains(graph.Evaluation.Id.ToString() + MainHub.CURRENT_MOVES_POSTFIX, left);
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

    /// <param name="claimsFromDatabase">
    /// Rebuild the caller's permission claims with the real <see cref="UserClaimsService"/>, as a connection does;
    /// otherwise they stay the principal's own.
    /// </param>
    private MainHub Hub(HubHarness harness, bool claimsFromDatabase = false)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped(_ => NewContext());
        services.AddSingleton(AuthorizationHarness.CreateFrameworkAuthorizationService());
        services.AddSingleton<IHttpContextAccessor>(new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = harness.User } });
        services.AddScoped<IIdentityResolver, IdentityResolver>();
        services.AddScoped<ICiteAuthorizationService, AuthorizationService>();
        services.AddSingleton<IMemoryCache>(new MemoryCache(new MemoryCacheOptions()));
        services.AddSingleton(new ClaimsTransformationOptions());
        if (claimsFromDatabase)
        {
            services.AddScoped<IUserClaimsService, UserClaimsService>();
        }
        else
        {
            var claims = Substitute.For<IUserClaimsService>();
            claims.RefreshClaims(Arg.Any<ClaimsPrincipal>()).Returns(call => call.Arg<ClaimsPrincipal>());
            services.AddSingleton(claims);
        }
        var provider = services.BuildServiceProvider();
        _providers.Add(provider);

        return harness.Attach(new MainHub(
            null,
            null,
            null,
            provider.GetRequiredService<IServiceScopeFactory>(),
            provider.GetRequiredService<ICiteAuthorizationService>(),
            provider.GetRequiredService<IUserClaimsService>(),
            provider.GetRequiredService<IHttpContextAccessor>()));
    }

    /// <summary>The groups the connection was added to, in order.</summary>
    private static string[] Joined(HubHarness harness) =>
        [.. harness.Groups.ReceivedCalls()
            .Where(x => x.GetMethodInfo().Name == nameof(IGroupManager.AddToGroupAsync))
            .Select(x => (string)x.GetArguments()[1])];
}
