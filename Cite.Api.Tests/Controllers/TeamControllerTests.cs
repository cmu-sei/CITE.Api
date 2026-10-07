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
/// <c>TeamController</c>: teams are read with a team permission or an evaluation-wide system permission, and
/// changed with EditEvaluation on their evaluation.
/// </summary>
public class TeamControllerTests(DatabaseFixture fixture, CiteAppFactory factory) : ApiTestBase(fixture, factory)
{
    /// <summary>A request with no identity is answered with a 401; the anonymous client is the case under test.</summary>
    [Fact]
    public async Task Get_without_an_identity_is_unauthorized()
    {
        await AssertStatus(HttpStatusCode.Unauthorized, await Client().GetAsync($"api/teams/{Guid.NewGuid()}", Ct));
    }

    [Fact]
    public async Task GetMineByEvaluation_lists_only_the_callers_team_to_a_member()
    {
        var graph = await Seed();
        var otherTeam = TestData.Team(graph.Evaluation.Id, graph.TeamType.Id, "Other Team");
        await Seed(otherTeam);
        var actor = await Actor().OnTeam(graph.Team.Id).SeedAsync();

        var teams = await ReadAsync<List<Team>>(await Client(actor).GetAsync($"api/evaluations/{graph.Evaluation.Id}/myteams", Ct));

        Assert.Equal([graph.Team.Id], teams.Select(x => x.Id));
    }

    [Fact]
    public async Task GetMineByEvaluation_lists_every_team_to_a_caller_holding_ObserveEvaluation_on_it()
    {
        var graph = await Seed();
        var otherTeam = TestData.Team(graph.Evaluation.Id, graph.TeamType.Id, "Other Team");
        await Seed(otherTeam);
        var actor = await Actor().OnEvaluation(graph.Evaluation.Id, permissions: [EvaluationPermission.ObserveEvaluation]).SeedAsync();

        var teams = await ReadAsync<List<Team>>(await Client(actor).GetAsync($"api/evaluations/{graph.Evaluation.Id}/myteams", Ct));

        Assert.Equal(new[] { graph.Team.Id, otherTeam.Id }.Order(), teams.Select(x => x.Id).Order());
    }

    [Fact]
    public async Task GetMineByEvaluation_lists_nothing_to_a_caller_holding_ObserveEvaluation_only_on_another_evaluation()
    {
        var graph = await Seed();
        var actor = await Actor().OnNewEvaluation(EvaluationPermission.ObserveEvaluation).SeedAsync();

        var teams = await ReadAsync<List<Team>>(await Client(actor).GetAsync($"api/evaluations/{graph.Evaluation.Id}/myteams", Ct));

        Assert.Empty(teams);
    }

    [Fact]
    public async Task GetByEvaluation_lists_the_teams_to_a_caller_holding_ParticipateInEvaluation_on_it()
    {
        var graph = await Seed();
        var actor = await Actor().OnEvaluation(graph.Evaluation.Id, permissions: [EvaluationPermission.ParticipateInEvaluation]).SeedAsync();

        var teams = await ReadAsync<List<Team>>(await Client(actor).GetAsync($"api/evaluations/{graph.Evaluation.Id}/teams", Ct));

        Assert.Equal(graph.Team.Id, Assert.Single(teams).Id);
    }

    [Fact]
    public async Task GetByEvaluation_is_forbidden_for_a_caller_holding_only_EditEvaluation()
    {
        var graph = await Seed();
        var actor = await Actor().OnEvaluation(graph.Evaluation.Id, permissions: [EvaluationPermission.EditEvaluation]).SeedAsync();

        await AssertStatus(HttpStatusCode.Forbidden, await Client(actor).GetAsync($"api/evaluations/{graph.Evaluation.Id}/teams", Ct));
    }

    [Fact]
    public async Task GetByEvaluation_is_forbidden_for_a_caller_holding_ViewEvaluation_only_on_another_evaluation()
    {
        var graph = await Seed();
        var actor = await Actor().OnNewEvaluation(EvaluationPermission.ViewEvaluation).SeedAsync();

        await AssertStatus(HttpStatusCode.Forbidden, await Client(actor).GetAsync($"api/evaluations/{graph.Evaluation.Id}/teams", Ct));
    }

    [Fact]
    public async Task Get_returns_the_team_with_its_memberships_to_a_caller_holding_ViewTeam_on_it()
    {
        var graph = await Seed();
        var actor = await Actor().OnTeam(graph.Team.Id, permissions: [TeamPermission.ViewTeam]).SeedAsync();

        var team = await ReadAsync<Team>(await Client(actor).GetAsync($"api/teams/{graph.Team.Id}", Ct));

        Assert.Equal(actor.Id, Assert.Single(team.Memberships).UserId);
    }

    [Fact]
    public async Task Get_returns_the_team_to_a_caller_holding_ObserveEvaluations()
    {
        var graph = await Seed();
        var actor = await Actor().WithSystemPermissions(SystemPermission.ObserveEvaluations).SeedAsync();

        await AssertStatus(HttpStatusCode.OK, await Client(actor).GetAsync($"api/teams/{graph.Team.Id}", Ct));
    }

    [Fact]
    public async Task Get_is_forbidden_for_a_caller_holding_ViewTeam_only_on_a_sibling_team()
    {
        var graph = await Seed();
        var actor = await Actor().OnNewTeam(graph.Evaluation.Id, TeamPermission.ViewTeam).SeedAsync();

        await AssertApiError(HttpStatusCode.Forbidden, await Client(actor).GetAsync($"api/teams/{graph.Team.Id}", Ct));
    }

    /// <summary>ViewEvaluation on the team's evaluation does not reach the team.</summary>
    [Fact]
    public async Task Get_is_forbidden_for_a_caller_holding_only_ViewEvaluation_on_its_evaluation()
    {
        var graph = await Seed();
        var actor = await Actor().OnEvaluation(graph.Evaluation.Id, permissions: [EvaluationPermission.ViewEvaluation]).SeedAsync();

        await AssertStatus(HttpStatusCode.Forbidden, await Client(actor).GetAsync($"api/teams/{graph.Team.Id}", Ct));
    }

    [Fact]
    public async Task Get_answers_an_unknown_team_with_not_found()
    {
        await AssertApiError(HttpStatusCode.NotFound, await RootClient.GetAsync($"api/teams/{Guid.NewGuid()}", Ct));
    }

    [Fact]
    public async Task Create_persists_the_team_and_a_submission_per_move_for_a_caller_holding_EditEvaluation_on_its_evaluation()
    {
        var graph = await Seed();
        var actor = await Actor().OnEvaluation(graph.Evaluation.Id, permissions: [EvaluationPermission.EditEvaluation]).SeedAsync();
        var id = Guid.NewGuid();

        var response = await Client(actor).PostAsJsonAsync("api/teams", NewTeam(id, graph), Ct);

        await AssertStatus(HttpStatusCode.Created, response);
        await using var context = NewContext();
        Assert.Equal("New Team", (await context.Teams.SingleAsync(x => x.Id == id, Ct)).Name);
        Assert.Equal(0, (await context.Submissions.SingleAsync(x => x.TeamId == id && x.UserId == null, Ct)).MoveNumber);
    }

    [Fact]
    public async Task Create_broadcasts_the_team_to_the_team_and_evaluation_groups()
    {
        var graph = await Seed();
        var id = Guid.NewGuid();

        await AssertStatus(HttpStatusCode.Created, await RootClient.PostAsJsonAsync("api/teams", NewTeam(id, graph), Ct));

        Assert.Single(Factory.Hub<MainHub>().ToGroup(id), x => x.Method == MainHubMethods.TeamCreated);
        var broadcast = Assert.Single(Factory.Hub<MainHub>().ToGroup(graph.Evaluation.Id), x => x.Method == MainHubMethods.TeamCreated);
        Assert.Equal(id, Assert.IsType<Team>(broadcast.Arguments[0]).Id);
    }

    [Fact]
    public async Task Create_is_forbidden_for_a_caller_holding_only_ViewEvaluation_on_the_evaluation()
    {
        var graph = await Seed();
        var actor = await Actor().OnEvaluation(graph.Evaluation.Id, permissions: [EvaluationPermission.ViewEvaluation]).SeedAsync();
        var id = Guid.NewGuid();

        await AssertStatus(HttpStatusCode.Forbidden, await Client(actor).PostAsJsonAsync("api/teams", NewTeam(id, graph), Ct));

        await using var context = NewContext();
        Assert.False(await context.Teams.AnyAsync(x => x.Id == id, Ct));
    }

    [Fact]
    public async Task Create_is_forbidden_for_a_caller_holding_EditEvaluation_only_on_another_evaluation()
    {
        var graph = await Seed();
        var actor = await Actor().OnNewEvaluation(EvaluationPermission.EditEvaluation).SeedAsync();

        await AssertStatus(HttpStatusCode.Forbidden, await Client(actor).PostAsJsonAsync("api/teams", NewTeam(Guid.NewGuid(), graph), Ct));
    }

    [Fact]
    public async Task Update_renames_the_team_for_a_caller_holding_EditEvaluation_on_its_evaluation()
    {
        var graph = await Seed();
        var actor = await Actor().OnEvaluation(graph.Evaluation.Id, permissions: [EvaluationPermission.EditEvaluation]).SeedAsync();

        var response = await Client(actor).PutAsJsonAsync($"api/teams/{graph.Team.Id}", NewTeam(graph.Team.Id, graph) with { Name = "Renamed" }, Ct);

        await AssertStatus(HttpStatusCode.OK, response);
        await using var context = NewContext();
        Assert.Equal("Renamed", (await context.Teams.SingleAsync(x => x.Id == graph.Team.Id, Ct)).Name);
    }

    [Fact]
    public async Task Update_is_forbidden_for_a_caller_holding_only_ManageTeam_on_the_team()
    {
        var graph = await Seed();
        var actor = await Actor().OnTeam(graph.Team.Id, permissions: [TeamPermission.ManageTeam]).SeedAsync();

        await AssertStatus(HttpStatusCode.Forbidden, await Client(actor).PutAsJsonAsync($"api/teams/{graph.Team.Id}", NewTeam(graph.Team.Id, graph) with { Name = "Renamed" }, Ct));

        await using var context = NewContext();
        Assert.Equal(graph.Team.Name, (await context.Teams.SingleAsync(x => x.Id == graph.Team.Id, Ct)).Name);
    }

    /// <summary>EditEvaluation on the evaluation named in the body moves a team of another evaluation into it.</summary>
    [Fact]
    public async Task Update_moves_a_team_of_another_evaluation_into_the_one_the_caller_edits()
    {
        var target = await Seed("Target");
        var actor = await Actor().OnNewEvaluation(EvaluationPermission.EditEvaluation).SeedAsync();
        var editable = actor.NewEvaluationIds[0];

        var response = await Client(actor).PutAsJsonAsync($"api/teams/{target.Team.Id}",
            new TeamBody(target.Team.Id, "Taken", "Taken", editable, target.TeamType.Id), Ct);

        await AssertStatus(HttpStatusCode.OK, response);
        await using var context = NewContext();
        Assert.Equal(editable, (await context.Teams.SingleAsync(x => x.Id == target.Team.Id, Ct)).EvaluationId);
    }

    [Fact]
    public async Task Delete_removes_the_team_and_its_submissions_for_a_caller_holding_EditEvaluation_on_its_evaluation()
    {
        var graph = await Seed();
        await Seed(TestData.Submission(graph.ScoringModel.Id, graph.Evaluation.Id, graph.Team.Id));
        var actor = await Actor().OnEvaluation(graph.Evaluation.Id, permissions: [EvaluationPermission.EditEvaluation]).SeedAsync();

        await AssertStatus(HttpStatusCode.NoContent, await Client(actor).DeleteAsync($"api/teams/{graph.Team.Id}", Ct));

        await using var context = NewContext();
        Assert.False(await context.Teams.AnyAsync(x => x.Id == graph.Team.Id, Ct));
        Assert.False(await context.Submissions.AnyAsync(x => x.TeamId == graph.Team.Id, Ct));
    }

    [Fact]
    public async Task Delete_broadcasts_the_id_to_the_team_group()
    {
        var graph = await Seed();

        await AssertStatus(HttpStatusCode.NoContent, await RootClient.DeleteAsync($"api/teams/{graph.Team.Id}", Ct));

        Assert.Equal(graph.Team.Id, Assert.Single(Factory.Hub<MainHub>().ToGroup(graph.Team.Id), x => x.Method == MainHubMethods.TeamDeleted).Argument);
    }

    [Fact]
    public async Task Delete_is_forbidden_for_a_caller_holding_only_ManageTeam_on_the_team()
    {
        var graph = await Seed();
        var actor = await Actor().OnTeam(graph.Team.Id, permissions: [TeamPermission.ManageTeam]).SeedAsync();

        await AssertStatus(HttpStatusCode.Forbidden, await Client(actor).DeleteAsync($"api/teams/{graph.Team.Id}", Ct));

        await using var context = NewContext();
        Assert.True(await context.Teams.AnyAsync(x => x.Id == graph.Team.Id, Ct));
    }

    [Fact]
    public async Task Delete_is_forbidden_for_a_caller_holding_EditEvaluation_only_on_another_evaluation()
    {
        var graph = await Seed();
        var actor = await Actor().OnNewEvaluation(EvaluationPermission.EditEvaluation).SeedAsync();

        await AssertStatus(HttpStatusCode.Forbidden, await Client(actor).DeleteAsync($"api/teams/{graph.Team.Id}", Ct));
    }

    /// <summary>A team with no evaluation is deleted and the request is then answered with a 500.</summary>
    [Fact]
    public async Task Delete_removes_a_team_without_an_evaluation_and_answers_with_a_server_error()
    {
        var teamType = TestData.TeamType();
        var team = TestData.Team(null, teamType.Id);
        await Seed(teamType, team);

        var error = await AssertApiError(HttpStatusCode.InternalServerError, await RootClient.DeleteAsync($"api/teams/{team.Id}", Ct));
        Assert.Equal("Nullable object must have a value.", error.Detail);

        await using var context = NewContext();
        Assert.False(await context.Teams.AnyAsync(x => x.Id == team.Id, Ct));
    }

    private Task<EvaluationGraph> Seed(string description = "Test Evaluation") =>
        TestScenario.SeedEvaluationAsync(Db, Ct, description);

    private static TeamBody NewTeam(Guid id, EvaluationGraph graph) =>
        new(id, "New Team", "NT", graph.Evaluation.Id, graph.TeamType.Id);

    private sealed record TeamBody(Guid Id, string Name, string ShortName, Guid? EvaluationId, Guid TeamTypeId);
}
