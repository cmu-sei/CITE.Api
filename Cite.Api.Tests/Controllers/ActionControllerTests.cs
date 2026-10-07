// Copyright 2026 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.

using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Cite.Api.Data.Enumerations;
using Cite.Api.Data.Models;
using Cite.Api.Hubs;
using Cite.Api.Tests.Support;
using Action = Cite.Api.ViewModels.Action;

namespace Cite.Api.Tests.Controllers;

/// <summary>
/// <c>ActionController</c>: a team's actions, read with a team or evaluation view permission and changed
/// with a team permission or EditEvaluation on the evaluation.
/// </summary>
public class ActionControllerTests(DatabaseFixture fixture, CiteAppFactory factory) : ApiTestBase(fixture, factory)
{
    /// <summary>A request with no identity is answered with a 401; the anonymous client is the case under test.</summary>
    [Fact]
    public async Task Get_without_an_identity_is_unauthorized()
    {
        await AssertStatus(HttpStatusCode.Unauthorized, await Client().GetAsync($"api/actions/{Guid.NewGuid()}", Ct));
    }

    [Fact]
    public async Task GetByEvaluation_lists_every_teams_actions_to_a_caller_holding_ViewEvaluation_on_it()
    {
        var (graph, action) = await SeedWithAction();
        var actor = await Actor().OnEvaluation(graph.Evaluation.Id, permissions: [EvaluationPermission.ViewEvaluation]).SeedAsync();

        var actions = await ReadAsync<List<Action>>(await Client(actor).GetAsync($"api/evaluations/{graph.Evaluation.Id}/actions", Ct));

        Assert.Equal(action.Id, Assert.Single(actions).Id);
    }

    [Fact]
    public async Task GetByEvaluation_is_forbidden_for_a_caller_holding_only_ObserveEvaluation()
    {
        var (graph, _) = await SeedWithAction();
        var actor = await Actor().OnEvaluation(graph.Evaluation.Id, permissions: [EvaluationPermission.ObserveEvaluation]).SeedAsync();

        await AssertApiError(HttpStatusCode.Forbidden, await Client(actor).GetAsync($"api/evaluations/{graph.Evaluation.Id}/actions", Ct));
    }

    [Fact]
    public async Task GetByEvaluationTeam_lists_the_teams_actions_to_a_caller_holding_ViewTeam_on_it()
    {
        var (graph, action) = await SeedWithAction();
        var actor = await Actor().OnTeam(graph.Team.Id, permissions: [TeamPermission.ViewTeam]).SeedAsync();

        var actions = await ReadAsync<List<Action>>(await Client(actor).GetAsync($"api/evaluations/{graph.Evaluation.Id}/teams/{graph.Team.Id}/actions", Ct));

        Assert.Equal(action.Id, Assert.Single(actions).Id);
    }

    [Fact]
    public async Task GetByEvaluationTeam_lists_the_teams_actions_to_a_caller_holding_ObserveEvaluation_on_the_evaluation()
    {
        var (graph, action) = await SeedWithAction();
        var actor = await Actor().OnEvaluation(graph.Evaluation.Id, permissions: [EvaluationPermission.ObserveEvaluation]).SeedAsync();

        var actions = await ReadAsync<List<Action>>(await Client(actor).GetAsync($"api/evaluations/{graph.Evaluation.Id}/teams/{graph.Team.Id}/actions", Ct));

        Assert.Equal(action.Id, Assert.Single(actions).Id);
    }

    [Fact]
    public async Task GetByEvaluationTeam_is_forbidden_for_a_caller_holding_ViewTeam_only_on_a_sibling_team()
    {
        var (graph, _) = await SeedWithAction();
        var actor = await Actor().OnNewTeam(graph.Evaluation.Id, TeamPermission.ViewTeam).SeedAsync();

        await AssertStatus(HttpStatusCode.Forbidden, await Client(actor).GetAsync($"api/evaluations/{graph.Evaluation.Id}/teams/{graph.Team.Id}/actions", Ct));
    }

    [Fact]
    public async Task GetByEvaluationMove_lists_the_moves_actions_to_a_caller_holding_ViewEvaluations()
    {
        var (graph, action) = await SeedWithAction();
        var actor = await Actor().WithSystemPermissions(SystemPermission.ViewEvaluations).SeedAsync();

        var actions = await ReadAsync<List<Action>>(await Client(actor).GetAsync($"api/evaluations/{graph.Evaluation.Id}/moves/0/actions", Ct));

        Assert.Equal(action.Id, Assert.Single(actions).Id);
    }

    [Fact]
    public async Task GetByEvaluationMove_is_forbidden_for_a_caller_holding_only_ViewTeam_on_one_of_its_teams()
    {
        var (graph, _) = await SeedWithAction();
        var actor = await Actor().OnTeam(graph.Team.Id, permissions: [TeamPermission.ViewTeam]).SeedAsync();

        await AssertStatus(HttpStatusCode.Forbidden, await Client(actor).GetAsync($"api/evaluations/{graph.Evaluation.Id}/moves/0/actions", Ct));
    }

    [Fact]
    public async Task GetByEvaluationMoveTeam_lists_the_teams_actions_for_the_move_to_a_caller_holding_ViewTeam_on_it()
    {
        var (graph, action) = await SeedWithAction();
        var actor = await Actor().OnTeam(graph.Team.Id, permissions: [TeamPermission.ViewTeam]).SeedAsync();

        var actions = await ReadAsync<List<Action>>(await Client(actor).GetAsync($"api/evaluations/{graph.Evaluation.Id}/moves/0/teams/{graph.Team.Id}/actions", Ct));

        Assert.Equal(action.Id, Assert.Single(actions).Id);
    }

    [Fact]
    public async Task GetByEvaluationMoveTeam_is_forbidden_for_a_caller_holding_ViewTeam_only_on_a_sibling_team()
    {
        var (graph, _) = await SeedWithAction();
        var actor = await Actor().OnNewTeam(graph.Evaluation.Id, TeamPermission.ViewTeam).SeedAsync();

        await AssertStatus(HttpStatusCode.Forbidden, await Client(actor).GetAsync($"api/evaluations/{graph.Evaluation.Id}/moves/0/teams/{graph.Team.Id}/actions", Ct));
    }

    [Fact]
    public async Task Get_returns_the_action_to_a_caller_holding_ViewTeam_on_its_team()
    {
        var (graph, action) = await SeedWithAction();
        var actor = await Actor().OnTeam(graph.Team.Id, permissions: [TeamPermission.ViewTeam]).SeedAsync();

        var read = await ReadAsync<Action>(await Client(actor).GetAsync($"api/actions/{action.Id}", Ct));

        Assert.Equal(action.Description, read.Description);
    }

    [Fact]
    public async Task Get_is_forbidden_for_a_caller_holding_ViewTeam_only_on_a_sibling_team()
    {
        var (graph, action) = await SeedWithAction();
        var actor = await Actor().OnNewTeam(graph.Evaluation.Id, TeamPermission.ViewTeam).SeedAsync();

        await AssertStatus(HttpStatusCode.Forbidden, await Client(actor).GetAsync($"api/actions/{action.Id}", Ct));
    }

    /// <summary>An unknown action id is answered with a 500 for a caller the gate lets through.</summary>
    [Fact]
    public async Task Get_answers_an_unknown_action_with_a_server_error()
    {
        var error = await AssertApiError(HttpStatusCode.InternalServerError, await RootClient.GetAsync($"api/actions/{Guid.NewGuid()}", Ct));
        Assert.Equal("Sequence contains no elements.", error.Detail);
    }

    [Fact]
    public async Task Create_persists_the_action_for_a_caller_holding_SubmitTeamScore_on_its_team()
    {
        var graph = await Seed();
        var actor = await Actor().OnTeam(graph.Team.Id, permissions: [TeamPermission.SubmitTeamScore]).SeedAsync();
        var id = Guid.NewGuid();

        var response = await Client(actor).PostAsJsonAsync("api/actions", NewAction(id, graph), Ct);

        await AssertStatus(HttpStatusCode.Created, response);
        await using var context = NewContext();
        Assert.Equal("New Action", (await context.Actions.SingleAsync(x => x.Id == id, Ct)).Description);
    }

    [Fact]
    public async Task Create_broadcasts_the_action_to_its_team()
    {
        var graph = await Seed();
        var id = Guid.NewGuid();

        await AssertStatus(HttpStatusCode.Created, await RootClient.PostAsJsonAsync("api/actions", NewAction(id, graph), Ct));

        var broadcast = Assert.Single(Factory.Hub<MainHub>().ToGroup(graph.Team.Id), x => x.Method == MainHubMethods.ActionCreated);
        Assert.Equal(id, Assert.IsType<Action>(broadcast.Arguments[0]).Id);
    }

    [Fact]
    public async Task Create_is_forbidden_for_a_caller_holding_only_EditTeamScore_on_its_team()
    {
        var graph = await Seed();
        var actor = await Actor().OnTeam(graph.Team.Id, permissions: [TeamPermission.EditTeamScore]).SeedAsync();
        var id = Guid.NewGuid();

        await AssertStatus(HttpStatusCode.Forbidden, await Client(actor).PostAsJsonAsync("api/actions", NewAction(id, graph), Ct));

        await using var context = NewContext();
        Assert.False(await context.Actions.AnyAsync(x => x.Id == id, Ct));
    }

    [Fact]
    public async Task Create_is_forbidden_for_a_caller_holding_SubmitTeamScore_only_on_a_sibling_team()
    {
        var graph = await Seed();
        var actor = await Actor().OnNewTeam(graph.Evaluation.Id, TeamPermission.SubmitTeamScore).SeedAsync();

        await AssertStatus(HttpStatusCode.Forbidden, await Client(actor).PostAsJsonAsync("api/actions", NewAction(Guid.NewGuid(), graph), Ct));
    }

    [Fact]
    public async Task Update_changes_the_action_for_a_caller_holding_ManageTeam_on_its_team()
    {
        var (graph, action) = await SeedWithAction();
        var actor = await Actor().OnTeam(graph.Team.Id, permissions: [TeamPermission.ManageTeam]).SeedAsync();

        var response = await Client(actor).PutAsJsonAsync($"api/actions/{action.Id}", NewAction(action.Id, graph) with { Description = "Changed" }, Ct);

        await AssertStatus(HttpStatusCode.OK, response);
        await using var context = NewContext();
        Assert.Equal("Changed", (await context.Actions.SingleAsync(x => x.Id == action.Id, Ct)).Description);
    }

    [Fact]
    public async Task Update_is_forbidden_for_a_caller_holding_only_SubmitTeamScore_on_its_team()
    {
        var (graph, action) = await SeedWithAction();
        var actor = await Actor().OnTeam(graph.Team.Id, permissions: [TeamPermission.SubmitTeamScore]).SeedAsync();

        await AssertStatus(HttpStatusCode.Forbidden, await Client(actor).PutAsJsonAsync($"api/actions/{action.Id}", NewAction(action.Id, graph) with { Description = "Changed" }, Ct));

        await using var context = NewContext();
        Assert.Equal(action.Description, (await context.Actions.SingleAsync(x => x.Id == action.Id, Ct)).Description);
    }

    /// <summary>ManageTeam on the team named in the body lets a caller rewrite another team's action and take it over.</summary>
    [Fact]
    public async Task Update_moves_another_teams_action_to_the_team_the_caller_manages()
    {
        var (graph, action) = await SeedWithAction();
        var actor = await Actor().OnNewTeam(graph.Evaluation.Id, TeamPermission.ManageTeam).SeedAsync();
        var managed = actor.NewTeamIds[0];

        var response = await Client(actor).PutAsJsonAsync($"api/actions/{action.Id}", NewAction(action.Id, graph) with { TeamId = managed }, Ct);

        await AssertStatus(HttpStatusCode.OK, response);
        await using var context = NewContext();
        Assert.Equal(managed, (await context.Actions.SingleAsync(x => x.Id == action.Id, Ct)).TeamId);
    }

    [Fact]
    public async Task Check_marks_the_action_checked_by_the_caller_holding_EditTeamScore_on_its_team()
    {
        var (graph, action) = await SeedWithAction();
        var actor = await Actor().OnTeam(graph.Team.Id, permissions: [TeamPermission.EditTeamScore]).SeedAsync();

        await AssertStatus(HttpStatusCode.OK, await Client(actor).PutAsync($"api/actions/{action.Id}/check", null, Ct));

        await using var context = NewContext();
        var stored = await context.Actions.SingleAsync(x => x.Id == action.Id, Ct);
        Assert.Equal((true, actor.Id), (stored.IsChecked, stored.ChangedBy.Value));
    }

    [Fact]
    public async Task Check_is_forbidden_for_a_caller_holding_only_ViewTeam_on_its_team()
    {
        var (graph, action) = await SeedWithAction();
        var actor = await Actor().OnTeam(graph.Team.Id, permissions: [TeamPermission.ViewTeam]).SeedAsync();

        await AssertStatus(HttpStatusCode.Forbidden, await Client(actor).PutAsync($"api/actions/{action.Id}/check", null, Ct));

        await using var context = NewContext();
        Assert.False((await context.Actions.SingleAsync(x => x.Id == action.Id, Ct)).IsChecked);
    }

    [Fact]
    public async Task Check_is_forbidden_for_a_caller_holding_EditTeamScore_only_on_a_sibling_team()
    {
        var (graph, action) = await SeedWithAction();
        var actor = await Actor().OnNewTeam(graph.Evaluation.Id, TeamPermission.EditTeamScore).SeedAsync();

        await AssertStatus(HttpStatusCode.Forbidden, await Client(actor).PutAsync($"api/actions/{action.Id}/check", null, Ct));
    }

    [Fact]
    public async Task Uncheck_clears_the_check_for_a_caller_holding_EditEvaluation_on_the_evaluation()
    {
        var (graph, action) = await SeedWithAction(isChecked: true);
        var actor = await Actor().OnEvaluation(graph.Evaluation.Id, permissions: [EvaluationPermission.EditEvaluation]).SeedAsync();

        await AssertStatus(HttpStatusCode.OK, await Client(actor).PutAsync($"api/actions/{action.Id}/uncheck", null, Ct));

        await using var context = NewContext();
        Assert.False((await context.Actions.SingleAsync(x => x.Id == action.Id, Ct)).IsChecked);
    }

    [Fact]
    public async Task Uncheck_is_forbidden_for_a_caller_holding_only_ViewTeam_on_its_team()
    {
        var (graph, action) = await SeedWithAction(isChecked: true);
        var actor = await Actor().OnTeam(graph.Team.Id, permissions: [TeamPermission.ViewTeam]).SeedAsync();

        await AssertStatus(HttpStatusCode.Forbidden, await Client(actor).PutAsync($"api/actions/{action.Id}/uncheck", null, Ct));

        await using var context = NewContext();
        Assert.True((await context.Actions.SingleAsync(x => x.Id == action.Id, Ct)).IsChecked);
    }

    [Fact]
    public async Task Delete_removes_the_action_for_a_caller_holding_ManageTeam_on_its_team()
    {
        var (graph, action) = await SeedWithAction();
        var actor = await Actor().OnTeam(graph.Team.Id, permissions: [TeamPermission.ManageTeam]).SeedAsync();

        await AssertStatus(HttpStatusCode.NoContent, await Client(actor).DeleteAsync($"api/actions/{action.Id}", Ct));

        await using var context = NewContext();
        Assert.False(await context.Actions.AnyAsync(x => x.Id == action.Id, Ct));
    }

    [Fact]
    public async Task Delete_broadcasts_the_id_to_the_team()
    {
        var (graph, action) = await SeedWithAction();

        await AssertStatus(HttpStatusCode.NoContent, await RootClient.DeleteAsync($"api/actions/{action.Id}", Ct));

        Assert.Equal(action.Id, Assert.Single(Factory.Hub<MainHub>().ToGroup(graph.Team.Id), x => x.Method == MainHubMethods.ActionDeleted).Argument);
    }

    [Fact]
    public async Task Delete_is_forbidden_for_a_caller_holding_only_EditTeamScore_on_its_team()
    {
        var (graph, action) = await SeedWithAction();
        var actor = await Actor().OnTeam(graph.Team.Id, permissions: [TeamPermission.EditTeamScore]).SeedAsync();

        await AssertStatus(HttpStatusCode.Forbidden, await Client(actor).DeleteAsync($"api/actions/{action.Id}", Ct));

        await using var context = NewContext();
        Assert.True(await context.Actions.AnyAsync(x => x.Id == action.Id, Ct));
    }

    [Fact]
    public async Task Create_persists_the_action_for_a_caller_holding_EditEvaluation_on_the_evaluation()
    {
        var graph = await Seed();
        var actor = await Actor().OnEvaluation(graph.Evaluation.Id, permissions: [EvaluationPermission.EditEvaluation]).SeedAsync();
        var id = Guid.NewGuid();

        await AssertStatus(HttpStatusCode.Created, await Client(actor).PostAsJsonAsync("api/actions", NewAction(id, graph), Ct));

        await using var context = NewContext();
        Assert.True(await context.Actions.AnyAsync(x => x.Id == id, Ct));
    }

    [Fact]
    public async Task Create_is_forbidden_for_a_caller_holding_only_ViewEvaluation_on_the_evaluation()
    {
        var graph = await Seed();
        var actor = await Actor().OnEvaluation(graph.Evaluation.Id, permissions: [EvaluationPermission.ViewEvaluation]).SeedAsync();
        var id = Guid.NewGuid();

        await AssertStatus(HttpStatusCode.Forbidden, await Client(actor).PostAsJsonAsync("api/actions", NewAction(id, graph), Ct));

        await using var context = NewContext();
        Assert.False(await context.Actions.AnyAsync(x => x.Id == id, Ct));
    }

    [Fact]
    public async Task Update_changes_the_action_for_a_caller_holding_EditEvaluation_on_the_evaluation()
    {
        var (graph, action) = await SeedWithAction();
        var actor = await Actor().OnEvaluation(graph.Evaluation.Id, permissions: [EvaluationPermission.EditEvaluation]).SeedAsync();

        await AssertStatus(HttpStatusCode.OK, await Client(actor).PutAsJsonAsync($"api/actions/{action.Id}", NewAction(action.Id, graph) with { Description = "Changed" }, Ct));

        await using var context = NewContext();
        Assert.Equal("Changed", (await context.Actions.SingleAsync(x => x.Id == action.Id, Ct)).Description);
    }

    [Fact]
    public async Task Update_is_forbidden_for_a_caller_holding_EditEvaluation_only_on_another_evaluation()
    {
        var (graph, action) = await SeedWithAction();
        var actor = await Actor().OnNewEvaluation(EvaluationPermission.EditEvaluation).SeedAsync();

        await AssertStatus(HttpStatusCode.Forbidden, await Client(actor).PutAsJsonAsync($"api/actions/{action.Id}", NewAction(action.Id, graph) with { Description = "Changed" }, Ct));

        await using var context = NewContext();
        Assert.Equal(action.Description, (await context.Actions.SingleAsync(x => x.Id == action.Id, Ct)).Description);
    }

    [Fact]
    public async Task Check_marks_the_action_checked_for_a_caller_holding_EditEvaluation_on_the_evaluation()
    {
        var (graph, action) = await SeedWithAction();
        var actor = await Actor().OnEvaluation(graph.Evaluation.Id, permissions: [EvaluationPermission.EditEvaluation]).SeedAsync();

        await AssertStatus(HttpStatusCode.OK, await Client(actor).PutAsync($"api/actions/{action.Id}/check", null, Ct));

        await using var context = NewContext();
        Assert.True((await context.Actions.SingleAsync(x => x.Id == action.Id, Ct)).IsChecked);
    }

    [Fact]
    public async Task Check_is_forbidden_for_a_caller_holding_only_ObserveEvaluation_on_the_evaluation()
    {
        var (graph, action) = await SeedWithAction();
        var actor = await Actor().OnEvaluation(graph.Evaluation.Id, permissions: [EvaluationPermission.ObserveEvaluation]).SeedAsync();

        await AssertStatus(HttpStatusCode.Forbidden, await Client(actor).PutAsync($"api/actions/{action.Id}/check", null, Ct));

        await using var context = NewContext();
        Assert.False((await context.Actions.SingleAsync(x => x.Id == action.Id, Ct)).IsChecked);
    }

    [Fact]
    public async Task Uncheck_clears_the_check_for_a_caller_holding_EditTeamScore_on_its_team()
    {
        var (graph, action) = await SeedWithAction(isChecked: true);
        var actor = await Actor().OnTeam(graph.Team.Id, permissions: [TeamPermission.EditTeamScore]).SeedAsync();

        await AssertStatus(HttpStatusCode.OK, await Client(actor).PutAsync($"api/actions/{action.Id}/uncheck", null, Ct));

        await using var context = NewContext();
        Assert.False((await context.Actions.SingleAsync(x => x.Id == action.Id, Ct)).IsChecked);
    }

    [Fact]
    public async Task Uncheck_is_forbidden_for_a_caller_holding_EditTeamScore_only_on_a_sibling_team()
    {
        var (graph, action) = await SeedWithAction(isChecked: true);
        var actor = await Actor().OnNewTeam(graph.Evaluation.Id, TeamPermission.EditTeamScore).SeedAsync();

        await AssertStatus(HttpStatusCode.Forbidden, await Client(actor).PutAsync($"api/actions/{action.Id}/uncheck", null, Ct));

        await using var context = NewContext();
        Assert.True((await context.Actions.SingleAsync(x => x.Id == action.Id, Ct)).IsChecked);
    }

    [Fact]
    public async Task Delete_removes_the_action_for_a_caller_holding_EditEvaluation_on_the_evaluation()
    {
        var (graph, action) = await SeedWithAction();
        var actor = await Actor().OnEvaluation(graph.Evaluation.Id, permissions: [EvaluationPermission.EditEvaluation]).SeedAsync();

        await AssertStatus(HttpStatusCode.NoContent, await Client(actor).DeleteAsync($"api/actions/{action.Id}", Ct));

        await using var context = NewContext();
        Assert.False(await context.Actions.AnyAsync(x => x.Id == action.Id, Ct));
    }

    [Fact]
    public async Task Delete_is_forbidden_for_a_caller_holding_EditEvaluation_only_on_another_evaluation()
    {
        var (_, action) = await SeedWithAction();
        var actor = await Actor().OnNewEvaluation(EvaluationPermission.EditEvaluation).SeedAsync();

        await AssertStatus(HttpStatusCode.Forbidden, await Client(actor).DeleteAsync($"api/actions/{action.Id}", Ct));

        await using var context = NewContext();
        Assert.True(await context.Actions.AnyAsync(x => x.Id == action.Id, Ct));
    }

    private Task<EvaluationGraph> Seed() => TestScenario.SeedEvaluationAsync(Db, Ct);

    private async Task<(EvaluationGraph Graph, ActionEntity Action)> SeedWithAction(bool isChecked = false)
    {
        var graph = await Seed();
        var action = TestData.Action(graph.Evaluation.Id, graph.Team.Id);
        action.IsChecked = isChecked;
        await Seed(action);

        return (graph, action);
    }

    private static ActionBody NewAction(Guid id, EvaluationGraph graph) =>
        new(id, graph.Evaluation.Id, graph.Team.Id, 0, 1, 1, "New Action");

    private sealed record ActionBody(Guid Id, Guid EvaluationId, Guid TeamId, int MoveNumber, int InjectNumber, int ActionNumber, string Description);
}
