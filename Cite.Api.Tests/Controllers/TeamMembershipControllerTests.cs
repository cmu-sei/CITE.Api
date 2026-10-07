// Copyright 2026 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.

using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Cite.Api.Data.Enumerations;
using Cite.Api.Data.Models;
using Cite.Api.Hubs;
using Cite.Api.Tests.Support;
using Cite.Api.ViewModels;

namespace Cite.Api.Tests.Controllers;

/// <summary>
/// <c>TeamMembershipsController</c>: memberships are read with ViewTeam (or view or observe on the team's
/// evaluation) and managed with ManageTeam (or ManageEvaluation on the team's evaluation).
/// </summary>
public class TeamMembershipControllerTests(DatabaseFixture fixture, CiteAppFactory factory) : ApiTestBase(fixture, factory)
{
    /// <summary>A request with no identity is answered with a 401; the anonymous client is the case under test.</summary>
    [Fact]
    public async Task GetAll_without_an_identity_is_unauthorized()
    {
        await AssertStatus(HttpStatusCode.Unauthorized, await Client().GetAsync($"api/teams/{Guid.NewGuid()}/memberships", Ct));
    }

    [Fact]
    public async Task GetAll_lists_the_memberships_to_a_caller_holding_ViewTeam_on_the_team()
    {
        var (graph, membership) = await SeedWithMember();
        var actor = await Actor().OnTeam(graph.Team.Id, permissions: [TeamPermission.ViewTeam]).SeedAsync();

        var memberships = await ReadAsync<List<TeamMembership>>(await Client(actor).GetAsync($"api/teams/{graph.Team.Id}/memberships", Ct));

        Assert.Contains(memberships, x => x.Id == membership.Id);
    }

    [Fact]
    public async Task GetAll_lists_the_memberships_to_a_caller_holding_ObserveEvaluation_on_the_teams_evaluation()
    {
        var (graph, membership) = await SeedWithMember();
        var actor = await Actor().OnEvaluation(graph.Evaluation.Id, permissions: [EvaluationPermission.ObserveEvaluation]).SeedAsync();

        var memberships = await ReadAsync<List<TeamMembership>>(await Client(actor).GetAsync($"api/teams/{graph.Team.Id}/memberships", Ct));

        Assert.Equal(membership.Id, Assert.Single(memberships).Id);
    }

    [Fact]
    public async Task GetAll_is_forbidden_for_a_caller_holding_ViewTeam_only_on_a_sibling_team()
    {
        var (graph, _) = await SeedWithMember();
        var actor = await Actor().OnNewTeam(graph.Evaluation.Id, TeamPermission.ViewTeam).SeedAsync();

        await AssertApiError(HttpStatusCode.Forbidden, await Client(actor).GetAsync($"api/teams/{graph.Team.Id}/memberships", Ct));
    }

    [Fact]
    public async Task GetAll_is_forbidden_for_a_caller_holding_only_EditEvaluation_on_the_teams_evaluation()
    {
        var (graph, _) = await SeedWithMember();
        var actor = await Actor().OnEvaluation(graph.Evaluation.Id, permissions: [EvaluationPermission.EditEvaluation]).SeedAsync();

        await AssertStatus(HttpStatusCode.Forbidden, await Client(actor).GetAsync($"api/teams/{graph.Team.Id}/memberships", Ct));
    }

    [Fact]
    public async Task Get_returns_the_membership_to_a_caller_holding_ViewTeam_on_the_team()
    {
        var (graph, membership) = await SeedWithMember();
        var actor = await Actor().OnTeam(graph.Team.Id, permissions: [TeamPermission.ViewTeam]).SeedAsync();

        var read = await ReadAsync<TeamMembership>(await Client(actor).GetAsync($"api/teams/memberships/{membership.Id}", Ct));

        Assert.Equal(membership.UserId, read.UserId);
    }

    [Fact]
    public async Task Get_is_forbidden_for_a_caller_holding_ViewTeam_only_on_a_sibling_team()
    {
        var (graph, membership) = await SeedWithMember();
        var actor = await Actor().OnNewTeam(graph.Evaluation.Id, TeamPermission.ViewTeam).SeedAsync();

        await AssertStatus(HttpStatusCode.Forbidden, await Client(actor).GetAsync($"api/teams/memberships/{membership.Id}", Ct));
    }

    [Fact]
    public async Task Get_answers_an_unknown_membership_with_not_found()
    {
        await AssertApiError(HttpStatusCode.NotFound, await RootClient.GetAsync($"api/teams/memberships/{Guid.NewGuid()}", Ct));
    }

    [Fact]
    public async Task CreateMembership_adds_the_user_and_their_submissions_for_a_caller_holding_ManageTeam_on_the_team()
    {
        var graph = await Seed();
        var user = TestData.User();
        await Seed(user);
        var actor = await Actor().OnTeam(graph.Team.Id, permissions: [TeamPermission.ManageTeam]).SeedAsync();

        var response = await Client(actor).PostAsJsonAsync($"api/teams/{graph.Team.Id}/memberships",
            new { teamId = graph.Team.Id, userId = user.Id, roleId = TestData.TeamRoles.Member }, Ct);

        await AssertStatus(HttpStatusCode.Created, response);
        await using var context = NewContext();
        Assert.Equal(TestData.TeamRoles.Member, (await context.TeamMemberships.SingleAsync(x => x.UserId == user.Id, Ct)).RoleId);
        Assert.Equal(0, (await context.Submissions.SingleAsync(x => x.UserId == user.Id, Ct)).MoveNumber);
    }

    [Fact]
    public async Task CreateMembership_adds_the_user_for_a_caller_holding_ManageEvaluation_on_the_teams_evaluation()
    {
        var graph = await Seed();
        var user = TestData.User();
        await Seed(user);
        var actor = await Actor().OnEvaluation(graph.Evaluation.Id, permissions: [EvaluationPermission.ManageEvaluation]).SeedAsync();

        var response = await Client(actor).PostAsJsonAsync($"api/teams/{graph.Team.Id}/memberships", new { teamId = graph.Team.Id, userId = user.Id }, Ct);

        await AssertStatus(HttpStatusCode.Created, response);
    }

    [Fact]
    public async Task CreateMembership_broadcasts_the_membership_to_the_team_and_the_user()
    {
        var graph = await Seed();
        var user = TestData.User();
        await Seed(user);

        await AssertStatus(HttpStatusCode.Created, await RootClient.PostAsJsonAsync($"api/teams/{graph.Team.Id}/memberships",
            new { teamId = graph.Team.Id, userId = user.Id }, Ct));

        var toUser = Assert.Single(Factory.Hub<MainHub>().ToGroup(user.Id), x => x.Method == MainHubMethods.TeamMembershipCreated);
        Assert.Equal(graph.Team.Id, Assert.IsType<TeamMembership>(toUser.Arguments[0]).TeamId);
        Assert.Single(Factory.Hub<MainHub>().ToGroup(graph.Team.Id), x => x.Method == MainHubMethods.TeamMembershipCreated);
    }

    [Fact]
    public async Task CreateMembership_is_forbidden_for_a_caller_holding_only_EditTeamScore_on_the_team()
    {
        var graph = await Seed();
        var user = TestData.User();
        await Seed(user);
        var actor = await Actor().OnTeam(graph.Team.Id, permissions: [TeamPermission.EditTeamScore]).SeedAsync();

        var response = await Client(actor).PostAsJsonAsync($"api/teams/{graph.Team.Id}/memberships", new { teamId = graph.Team.Id, userId = user.Id }, Ct);

        await AssertStatus(HttpStatusCode.Forbidden, response);
        await using var context = NewContext();
        Assert.False(await context.TeamMemberships.AnyAsync(x => x.UserId == user.Id, Ct));
    }

    [Fact]
    public async Task CreateMembership_is_forbidden_for_a_caller_holding_ManageTeam_only_on_a_sibling_team()
    {
        var graph = await Seed();
        var user = TestData.User();
        await Seed(user);
        var actor = await Actor().OnNewTeam(graph.Evaluation.Id, TeamPermission.ManageTeam).SeedAsync();

        await AssertStatus(HttpStatusCode.Forbidden, await Client(actor).PostAsJsonAsync($"api/teams/{graph.Team.Id}/memberships",
            new { teamId = graph.Team.Id, userId = user.Id }, Ct));
    }

    /// <summary>ManageTeam on the team named in the body adds the user to that team, whatever team the route names.</summary>
    [Fact]
    public async Task CreateMembership_adds_the_user_to_the_team_in_the_body_rather_than_the_route()
    {
        var graph = await Seed();
        var user = TestData.User();
        await Seed(user);
        var actor = await Actor().OnNewTeam(graph.Evaluation.Id, TeamPermission.ManageTeam).SeedAsync();
        var managed = actor.NewTeamIds[0];

        var response = await Client(actor).PostAsJsonAsync($"api/teams/{graph.Team.Id}/memberships", new { teamId = managed, userId = user.Id }, Ct);

        await AssertStatus(HttpStatusCode.Created, response);
        await using var context = NewContext();
        Assert.Equal(managed, (await context.TeamMemberships.SingleAsync(x => x.UserId == user.Id, Ct)).TeamId);
    }

    /// <summary>Adding a user to a team they are already on is answered with a 500.</summary>
    [Fact]
    public async Task CreateMembership_answers_a_duplicate_member_with_a_server_error()
    {
        var (graph, membership) = await SeedWithMember();

        var response = await RootClient.PostAsJsonAsync($"api/teams/{graph.Team.Id}/memberships", new { teamId = graph.Team.Id, userId = membership.UserId }, Ct);

        var error = await AssertApiError(HttpStatusCode.InternalServerError, response);
        Assert.StartsWith("An error occurred while saving the entity changes.", error.Detail);
    }

    [Fact]
    public async Task Update_changes_the_role_for_a_caller_holding_ManageTeam_on_the_team()
    {
        var (graph, membership) = await SeedWithMember();
        var actor = await Actor().OnTeam(graph.Team.Id, permissions: [TeamPermission.ManageTeam]).SeedAsync();

        var response = await Client(actor).PutAsJsonAsync($"api/teams/memberships/{membership.Id}",
            new { id = membership.Id, teamId = graph.Team.Id, userId = membership.UserId, roleId = TestData.TeamRoles.Owner }, Ct);

        await AssertStatus(HttpStatusCode.OK, response);
        await using var context = NewContext();
        Assert.Equal(TestData.TeamRoles.Owner, (await context.TeamMemberships.SingleAsync(x => x.Id == membership.Id, Ct)).RoleId);
    }

    [Fact]
    public async Task Update_is_forbidden_for_a_caller_holding_only_SubmitTeamScore_on_the_team()
    {
        var (graph, membership) = await SeedWithMember();
        var actor = await Actor().OnTeam(graph.Team.Id, permissions: [TeamPermission.SubmitTeamScore]).SeedAsync();

        var response = await Client(actor).PutAsJsonAsync($"api/teams/memberships/{membership.Id}",
            new { id = membership.Id, teamId = graph.Team.Id, userId = membership.UserId, roleId = TestData.TeamRoles.Owner }, Ct);

        await AssertStatus(HttpStatusCode.Forbidden, response);
        await using var context = NewContext();
        Assert.Equal(TestData.TeamRoles.Member, (await context.TeamMemberships.SingleAsync(x => x.Id == membership.Id, Ct)).RoleId);
    }

    [Fact]
    public async Task DeleteMembership_removes_the_membership_for_a_caller_holding_ManageTeam_on_the_team()
    {
        var (graph, membership) = await SeedWithMember();
        var actor = await Actor().OnTeam(graph.Team.Id, permissions: [TeamPermission.ManageTeam]).SeedAsync();

        await AssertStatus(HttpStatusCode.NoContent, await Client(actor).DeleteAsync($"api/teams/memberships/{membership.Id}", Ct));

        await using var context = NewContext();
        Assert.False(await context.TeamMemberships.AnyAsync(x => x.Id == membership.Id, Ct));
    }

    [Fact]
    public async Task DeleteMembership_is_forbidden_for_a_caller_holding_ManageTeam_only_on_a_sibling_team()
    {
        var (graph, membership) = await SeedWithMember();
        var actor = await Actor().OnNewTeam(graph.Evaluation.Id, TeamPermission.ManageTeam).SeedAsync();

        await AssertStatus(HttpStatusCode.Forbidden, await Client(actor).DeleteAsync($"api/teams/memberships/{membership.Id}", Ct));

        await using var context = NewContext();
        Assert.True(await context.TeamMemberships.AnyAsync(x => x.Id == membership.Id, Ct));
    }

    /// <summary>The deleted membership is broadcast to the team as the whole entity.</summary>
    [Fact]
    public async Task DeleteMembership_broadcasts_the_deleted_entity_to_the_team()
    {
        var (graph, membership) = await SeedWithMember();

        await AssertStatus(HttpStatusCode.NoContent, await RootClient.DeleteAsync($"api/teams/memberships/{membership.Id}", Ct));

        var broadcast = Assert.Single(Factory.Hub<MainHub>().ToGroup(graph.Team.Id), x => x.Method == MainHubMethods.TeamMembershipDeleted);
        Assert.Equal(membership.Id, Assert.IsType<TeamMembershipEntity>(broadcast.Argument).Id);
    }

    [Fact]
    public async Task Update_changes_the_role_for_a_caller_holding_ManageEvaluation_on_the_teams_evaluation()
    {
        var (graph, membership) = await SeedWithMember();
        var actor = await Actor().OnEvaluation(graph.Evaluation.Id, permissions: [EvaluationPermission.ManageEvaluation]).SeedAsync();

        await AssertStatus(HttpStatusCode.OK, await Client(actor).PutAsJsonAsync($"api/teams/memberships/{membership.Id}",
            new { id = membership.Id, teamId = graph.Team.Id, userId = membership.UserId, roleId = TestData.TeamRoles.Owner }, Ct));

        await using var context = NewContext();
        Assert.Equal(TestData.TeamRoles.Owner, (await context.TeamMemberships.SingleAsync(x => x.Id == membership.Id, Ct)).RoleId);
    }

    [Fact]
    public async Task Update_is_forbidden_for_a_caller_holding_only_EditEvaluation_on_the_teams_evaluation()
    {
        var (graph, membership) = await SeedWithMember();
        var actor = await Actor().OnEvaluation(graph.Evaluation.Id, permissions: [EvaluationPermission.EditEvaluation]).SeedAsync();

        await AssertStatus(HttpStatusCode.Forbidden, await Client(actor).PutAsJsonAsync($"api/teams/memberships/{membership.Id}",
            new { id = membership.Id, teamId = graph.Team.Id, userId = membership.UserId, roleId = TestData.TeamRoles.Owner }, Ct));

        await using var context = NewContext();
        Assert.Equal(TestData.TeamRoles.Member, (await context.TeamMemberships.SingleAsync(x => x.Id == membership.Id, Ct)).RoleId);
    }

    [Fact]
    public async Task DeleteMembership_removes_the_membership_for_a_caller_holding_ManageEvaluation_on_the_teams_evaluation()
    {
        var (graph, membership) = await SeedWithMember();
        var actor = await Actor().OnEvaluation(graph.Evaluation.Id, permissions: [EvaluationPermission.ManageEvaluation]).SeedAsync();

        await AssertStatus(HttpStatusCode.NoContent, await Client(actor).DeleteAsync($"api/teams/memberships/{membership.Id}", Ct));

        await using var context = NewContext();
        Assert.False(await context.TeamMemberships.AnyAsync(x => x.Id == membership.Id, Ct));
    }

    [Fact]
    public async Task DeleteMembership_is_forbidden_for_a_caller_holding_only_EditEvaluation_on_the_teams_evaluation()
    {
        var (graph, membership) = await SeedWithMember();
        var actor = await Actor().OnEvaluation(graph.Evaluation.Id, permissions: [EvaluationPermission.EditEvaluation]).SeedAsync();

        await AssertStatus(HttpStatusCode.Forbidden, await Client(actor).DeleteAsync($"api/teams/memberships/{membership.Id}", Ct));

        await using var context = NewContext();
        Assert.True(await context.TeamMemberships.AnyAsync(x => x.Id == membership.Id, Ct));
    }

    private Task<EvaluationGraph> Seed() => TestScenario.SeedEvaluationAsync(Db, Ct);

    private async Task<(EvaluationGraph Graph, TeamMembershipEntity Membership)> SeedWithMember()
    {
        var graph = await Seed();
        var user = TestData.User(name: "Member");
        var membership = TestData.TeamMembership(graph.Team.Id, user.Id, TestData.TeamRoles.Member);
        await Seed(user, membership);

        return (graph, membership);
    }
}
