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
/// <c>DutyController</c>: a team's duties and the users assigned to them, read with a team or evaluation
/// view permission and changed with a team permission or EditEvaluation on the evaluation.
/// </summary>
public class DutyControllerTests(DatabaseFixture fixture, CiteAppFactory factory) : ApiTestBase(fixture, factory)
{
    /// <summary>A request with no identity is answered with a 401; the anonymous client is the case under test.</summary>
    [Fact]
    public async Task Get_without_an_identity_is_unauthorized()
    {
        await AssertStatus(HttpStatusCode.Unauthorized, await Client().GetAsync($"api/duties/{Guid.NewGuid()}", Ct));
    }

    [Fact]
    public async Task GetByEvaluation_lists_the_duties_with_their_users_to_a_caller_holding_ViewEvaluation_on_it()
    {
        var (graph, duty, assignee) = await SeedWithAssignedDuty();
        var actor = await Actor().OnEvaluation(graph.Evaluation.Id, permissions: [EvaluationPermission.ViewEvaluation]).SeedAsync();

        var duties = await ReadAsync<List<Duty>>(await Client(actor).GetAsync($"api/evaluations/{graph.Evaluation.Id}/duties", Ct));

        var read = Assert.Single(duties);
        Assert.Equal((duty.Id, assignee.Id), (read.Id, Assert.Single(read.Users).Id));
    }

    [Fact]
    public async Task GetByEvaluation_is_forbidden_for_a_caller_holding_only_ObserveEvaluation()
    {
        var (graph, _, _) = await SeedWithAssignedDuty();
        var actor = await Actor().OnEvaluation(graph.Evaluation.Id, permissions: [EvaluationPermission.ObserveEvaluation]).SeedAsync();

        await AssertApiError(HttpStatusCode.Forbidden, await Client(actor).GetAsync($"api/evaluations/{graph.Evaluation.Id}/duties", Ct));
    }

    [Fact]
    public async Task GetByEvaluationTeam_lists_the_teams_duties_to_a_caller_holding_ViewTeam_on_it()
    {
        var (graph, duty, _) = await SeedWithAssignedDuty();
        var actor = await Actor().OnTeam(graph.Team.Id, permissions: [TeamPermission.ViewTeam]).SeedAsync();

        var duties = await ReadAsync<List<Duty>>(await Client(actor).GetAsync($"api/evaluations/{graph.Evaluation.Id}/teams/{graph.Team.Id}/duties", Ct));

        Assert.Equal(duty.Id, Assert.Single(duties).Id);
    }

    [Fact]
    public async Task GetByEvaluationTeam_is_forbidden_for_a_caller_holding_ViewTeam_only_on_a_sibling_team()
    {
        var (graph, _, _) = await SeedWithAssignedDuty();
        var actor = await Actor().OnNewTeam(graph.Evaluation.Id, TeamPermission.ViewTeam).SeedAsync();

        await AssertStatus(HttpStatusCode.Forbidden, await Client(actor).GetAsync($"api/evaluations/{graph.Evaluation.Id}/teams/{graph.Team.Id}/duties", Ct));
    }

    [Fact]
    public async Task Get_returns_the_duty_to_a_caller_holding_ViewTeam_on_its_team()
    {
        var (graph, duty, _) = await SeedWithAssignedDuty();
        var actor = await Actor().OnTeam(graph.Team.Id, permissions: [TeamPermission.ViewTeam]).SeedAsync();

        var read = await ReadAsync<Duty>(await Client(actor).GetAsync($"api/duties/{duty.Id}", Ct));

        Assert.Equal(duty.Name, read.Name);
    }

    [Fact]
    public async Task Get_is_forbidden_for_a_caller_holding_ViewTeam_only_on_a_sibling_team()
    {
        var (graph, duty, _) = await SeedWithAssignedDuty();
        var actor = await Actor().OnNewTeam(graph.Evaluation.Id, TeamPermission.ViewTeam).SeedAsync();

        await AssertStatus(HttpStatusCode.Forbidden, await Client(actor).GetAsync($"api/duties/{duty.Id}", Ct));
    }

    /// <summary>An unknown duty id is answered with a 500 for a caller the gate lets through.</summary>
    [Fact]
    public async Task Get_answers_an_unknown_duty_with_a_server_error()
    {
        var error = await AssertApiError(HttpStatusCode.InternalServerError, await RootClient.GetAsync($"api/duties/{Guid.NewGuid()}", Ct));
        Assert.Equal("Sequence contains no elements.", error.Detail);
    }

    [Fact]
    public async Task Create_persists_the_duty_for_a_caller_holding_SubmitTeamScore_on_its_team()
    {
        var graph = await Seed();
        var actor = await Actor().OnTeam(graph.Team.Id, permissions: [TeamPermission.SubmitTeamScore]).SeedAsync();
        var id = Guid.NewGuid();

        var response = await Client(actor).PostAsJsonAsync("api/duties", new { id, evaluationId = graph.Evaluation.Id, teamId = graph.Team.Id, name = "Scribe" }, Ct);

        await AssertStatus(HttpStatusCode.Created, response);
        await using var context = NewContext();
        Assert.Equal("Scribe", (await context.Duties.SingleAsync(x => x.Id == id, Ct)).Name);
    }

    /// <summary>Creating a duty broadcasts nothing to the team.</summary>
    [Fact]
    public async Task Create_broadcasts_nothing_to_the_team()
    {
        var graph = await Seed();

        await AssertStatus(HttpStatusCode.Created, await RootClient.PostAsJsonAsync("api/duties",
            new { id = Guid.NewGuid(), evaluationId = graph.Evaluation.Id, teamId = graph.Team.Id, name = "Scribe" }, Ct));

        Assert.Empty(Factory.Hub<MainHub>().ToGroup(graph.Team.Id));
    }

    [Fact]
    public async Task Create_is_forbidden_for_a_caller_holding_only_EditTeamScore_on_its_team()
    {
        var graph = await Seed();
        var actor = await Actor().OnTeam(graph.Team.Id, permissions: [TeamPermission.EditTeamScore]).SeedAsync();
        var id = Guid.NewGuid();

        await AssertStatus(HttpStatusCode.Forbidden, await Client(actor).PostAsJsonAsync("api/duties",
            new { id, evaluationId = graph.Evaluation.Id, teamId = graph.Team.Id, name = "Scribe" }, Ct));

        await using var context = NewContext();
        Assert.False(await context.Duties.AnyAsync(x => x.Id == id, Ct));
    }

    [Fact]
    public async Task Update_renames_the_duty_for_a_caller_holding_ManageTeam_on_its_team()
    {
        var (graph, duty, _) = await SeedWithAssignedDuty();
        var actor = await Actor().OnTeam(graph.Team.Id, permissions: [TeamPermission.ManageTeam]).SeedAsync();

        var response = await Client(actor).PutAsJsonAsync($"api/duties/{duty.Id}", new { id = duty.Id, evaluationId = graph.Evaluation.Id, teamId = graph.Team.Id, name = "Renamed" }, Ct);

        await AssertStatus(HttpStatusCode.OK, response);
        await using var context = NewContext();
        Assert.Equal("Renamed", (await context.Duties.SingleAsync(x => x.Id == duty.Id, Ct)).Name);
    }

    [Fact]
    public async Task Update_is_forbidden_for_a_caller_holding_only_SubmitTeamScore_on_its_team()
    {
        var (graph, duty, _) = await SeedWithAssignedDuty();
        var actor = await Actor().OnTeam(graph.Team.Id, permissions: [TeamPermission.SubmitTeamScore]).SeedAsync();

        await AssertStatus(HttpStatusCode.Forbidden, await Client(actor).PutAsJsonAsync($"api/duties/{duty.Id}",
            new { id = duty.Id, evaluationId = graph.Evaluation.Id, teamId = graph.Team.Id, name = "Renamed" }, Ct));

        await using var context = NewContext();
        Assert.Equal(duty.Name, (await context.Duties.SingleAsync(x => x.Id == duty.Id, Ct)).Name);
    }

    /// <summary>ManageTeam on the team named in the body lets a caller rename another team's duty.</summary>
    [Fact]
    public async Task Update_renames_another_teams_duty_for_a_caller_managing_the_team_in_the_body()
    {
        var (graph, duty, _) = await SeedWithAssignedDuty();
        var actor = await Actor().OnNewTeam(graph.Evaluation.Id, TeamPermission.ManageTeam).SeedAsync();

        var response = await Client(actor).PutAsJsonAsync($"api/duties/{duty.Id}",
            new { id = duty.Id, evaluationId = graph.Evaluation.Id, teamId = actor.NewTeamIds[0], name = "Taken" }, Ct);

        await AssertStatus(HttpStatusCode.OK, response);
        await using var context = NewContext();
        Assert.Equal("Taken", (await context.Duties.SingleAsync(x => x.Id == duty.Id, Ct)).Name);
    }

    [Fact]
    public async Task AddUserToDuty_assigns_the_user_for_a_caller_holding_EditTeamScore_on_its_team()
    {
        var graph = await Seed();
        var duty = TestData.Duty(graph.Evaluation.Id, graph.Team.Id);
        var user = TestData.User();
        await Seed(duty, user);
        var actor = await Actor().OnTeam(graph.Team.Id, permissions: [TeamPermission.EditTeamScore]).SeedAsync();

        await AssertStatus(HttpStatusCode.OK, await Client(actor).PutAsync($"api/duties/{duty.Id}/users/{user.Id}/add", null, Ct));

        await using var context = NewContext();
        Assert.True(await context.DutyUsers.AnyAsync(x => x.DutyId == duty.Id && x.UserId == user.Id, Ct));
    }

    [Fact]
    public async Task AddUserToDuty_is_forbidden_for_a_caller_holding_EditTeamScore_only_on_a_sibling_team()
    {
        var graph = await Seed();
        var duty = TestData.Duty(graph.Evaluation.Id, graph.Team.Id);
        var user = TestData.User();
        await Seed(duty, user);
        var actor = await Actor().OnNewTeam(graph.Evaluation.Id, TeamPermission.EditTeamScore).SeedAsync();

        await AssertStatus(HttpStatusCode.Forbidden, await Client(actor).PutAsync($"api/duties/{duty.Id}/users/{user.Id}/add", null, Ct));

        await using var context = NewContext();
        Assert.False(await context.DutyUsers.AnyAsync(x => x.DutyId == duty.Id, Ct));
    }

    /// <summary>Assigning a user already on the duty is answered with a 500.</summary>
    [Fact]
    public async Task AddUserToDuty_answers_a_user_already_assigned_with_a_server_error()
    {
        var (_, duty, assignee) = await SeedWithAssignedDuty();

        var error = await AssertApiError(HttpStatusCode.InternalServerError, await RootClient.PutAsync($"api/duties/{duty.Id}/users/{assignee.Id}/add", null, Ct));
        Assert.StartsWith("An error occurred while saving the entity changes.", error.Detail);
    }

    [Fact]
    public async Task AddUserToDuty_answers_an_unknown_user_with_not_found()
    {
        var (_, duty, _) = await SeedWithAssignedDuty();

        await AssertApiError(HttpStatusCode.NotFound, await RootClient.PutAsync($"api/duties/{duty.Id}/users/{Guid.NewGuid()}/add", null, Ct));
    }

    [Fact]
    public async Task RemoveUserFromDuty_unassigns_the_user_for_a_caller_holding_EditTeamScore_on_its_team()
    {
        var (graph, duty, assignee) = await SeedWithAssignedDuty();
        var actor = await Actor().OnTeam(graph.Team.Id, permissions: [TeamPermission.EditTeamScore]).SeedAsync();

        await AssertStatus(HttpStatusCode.OK, await Client(actor).PutAsync($"api/duties/{duty.Id}/users/{assignee.Id}/remove", null, Ct));

        await using var context = NewContext();
        Assert.False(await context.DutyUsers.AnyAsync(x => x.DutyId == duty.Id, Ct));
    }

    [Fact]
    public async Task RemoveUserFromDuty_is_forbidden_for_a_caller_holding_only_ViewTeam_on_its_team()
    {
        var (graph, duty, assignee) = await SeedWithAssignedDuty();
        var actor = await Actor().OnTeam(graph.Team.Id, permissions: [TeamPermission.ViewTeam]).SeedAsync();

        await AssertStatus(HttpStatusCode.Forbidden, await Client(actor).PutAsync($"api/duties/{duty.Id}/users/{assignee.Id}/remove", null, Ct));

        await using var context = NewContext();
        Assert.True(await context.DutyUsers.AnyAsync(x => x.DutyId == duty.Id, Ct));
    }

    [Fact]
    public async Task Delete_removes_the_duty_and_its_assignments_for_a_caller_holding_ManageTeam_on_its_team()
    {
        var (graph, duty, _) = await SeedWithAssignedDuty();
        var actor = await Actor().OnTeam(graph.Team.Id, permissions: [TeamPermission.ManageTeam]).SeedAsync();

        await AssertStatus(HttpStatusCode.NoContent, await Client(actor).DeleteAsync($"api/duties/{duty.Id}", Ct));

        await using var context = NewContext();
        Assert.False(await context.Duties.AnyAsync(x => x.Id == duty.Id, Ct));
        Assert.False(await context.DutyUsers.AnyAsync(x => x.DutyId == duty.Id, Ct));
    }

    [Fact]
    public async Task Delete_is_forbidden_for_a_caller_holding_only_EditTeamScore_on_its_team()
    {
        var (graph, duty, _) = await SeedWithAssignedDuty();
        var actor = await Actor().OnTeam(graph.Team.Id, permissions: [TeamPermission.EditTeamScore]).SeedAsync();

        await AssertStatus(HttpStatusCode.Forbidden, await Client(actor).DeleteAsync($"api/duties/{duty.Id}", Ct));

        await using var context = NewContext();
        Assert.True(await context.Duties.AnyAsync(x => x.Id == duty.Id, Ct));
    }

    [Fact]
    public async Task Create_persists_the_duty_for_a_caller_holding_EditEvaluation_on_the_evaluation()
    {
        var graph = await Seed();
        var actor = await Actor().OnEvaluation(graph.Evaluation.Id, permissions: [EvaluationPermission.EditEvaluation]).SeedAsync();
        var id = Guid.NewGuid();

        await AssertStatus(HttpStatusCode.Created, await Client(actor).PostAsJsonAsync("api/duties",
            new { id, evaluationId = graph.Evaluation.Id, teamId = graph.Team.Id, name = "Scribe" }, Ct));

        await using var context = NewContext();
        Assert.True(await context.Duties.AnyAsync(x => x.Id == id, Ct));
    }

    [Fact]
    public async Task Create_is_forbidden_for_a_caller_holding_EditEvaluation_only_on_another_evaluation()
    {
        var graph = await Seed();
        var actor = await Actor().OnNewEvaluation(EvaluationPermission.EditEvaluation).SeedAsync();
        var id = Guid.NewGuid();

        await AssertStatus(HttpStatusCode.Forbidden, await Client(actor).PostAsJsonAsync("api/duties",
            new { id, evaluationId = graph.Evaluation.Id, teamId = graph.Team.Id, name = "Scribe" }, Ct));

        await using var context = NewContext();
        Assert.False(await context.Duties.AnyAsync(x => x.Id == id, Ct));
    }

    [Fact]
    public async Task Update_renames_the_duty_for_a_caller_holding_EditEvaluation_on_the_evaluation()
    {
        var (graph, duty, _) = await SeedWithAssignedDuty();
        var actor = await Actor().OnEvaluation(graph.Evaluation.Id, permissions: [EvaluationPermission.EditEvaluation]).SeedAsync();

        await AssertStatus(HttpStatusCode.OK, await Client(actor).PutAsJsonAsync($"api/duties/{duty.Id}",
            new { id = duty.Id, evaluationId = graph.Evaluation.Id, teamId = graph.Team.Id, name = "Renamed" }, Ct));

        await using var context = NewContext();
        Assert.Equal("Renamed", (await context.Duties.SingleAsync(x => x.Id == duty.Id, Ct)).Name);
    }

    [Fact]
    public async Task Update_is_forbidden_for_a_caller_holding_only_ViewEvaluation_on_the_evaluation()
    {
        var (graph, duty, _) = await SeedWithAssignedDuty();
        var actor = await Actor().OnEvaluation(graph.Evaluation.Id, permissions: [EvaluationPermission.ViewEvaluation]).SeedAsync();

        await AssertStatus(HttpStatusCode.Forbidden, await Client(actor).PutAsJsonAsync($"api/duties/{duty.Id}",
            new { id = duty.Id, evaluationId = graph.Evaluation.Id, teamId = graph.Team.Id, name = "Renamed" }, Ct));

        await using var context = NewContext();
        Assert.Equal(duty.Name, (await context.Duties.SingleAsync(x => x.Id == duty.Id, Ct)).Name);
    }

    [Fact]
    public async Task AddUserToDuty_assigns_the_user_for_a_caller_holding_EditEvaluation_on_the_evaluation()
    {
        var graph = await Seed();
        var duty = TestData.Duty(graph.Evaluation.Id, graph.Team.Id);
        var user = TestData.User();
        await Seed(duty, user);
        var actor = await Actor().OnEvaluation(graph.Evaluation.Id, permissions: [EvaluationPermission.EditEvaluation]).SeedAsync();

        await AssertStatus(HttpStatusCode.OK, await Client(actor).PutAsync($"api/duties/{duty.Id}/users/{user.Id}/add", null, Ct));

        await using var context = NewContext();
        Assert.True(await context.DutyUsers.AnyAsync(x => x.DutyId == duty.Id && x.UserId == user.Id, Ct));
    }

    [Fact]
    public async Task AddUserToDuty_is_forbidden_for_a_caller_holding_EditEvaluation_only_on_another_evaluation()
    {
        var graph = await Seed();
        var duty = TestData.Duty(graph.Evaluation.Id, graph.Team.Id);
        var user = TestData.User();
        await Seed(duty, user);
        var actor = await Actor().OnNewEvaluation(EvaluationPermission.EditEvaluation).SeedAsync();

        await AssertStatus(HttpStatusCode.Forbidden, await Client(actor).PutAsync($"api/duties/{duty.Id}/users/{user.Id}/add", null, Ct));

        await using var context = NewContext();
        Assert.False(await context.DutyUsers.AnyAsync(x => x.DutyId == duty.Id, Ct));
    }

    [Fact]
    public async Task RemoveUserFromDuty_unassigns_the_user_for_a_caller_holding_EditEvaluation_on_the_evaluation()
    {
        var (graph, duty, assignee) = await SeedWithAssignedDuty();
        var actor = await Actor().OnEvaluation(graph.Evaluation.Id, permissions: [EvaluationPermission.EditEvaluation]).SeedAsync();

        await AssertStatus(HttpStatusCode.OK, await Client(actor).PutAsync($"api/duties/{duty.Id}/users/{assignee.Id}/remove", null, Ct));

        await using var context = NewContext();
        Assert.False(await context.DutyUsers.AnyAsync(x => x.DutyId == duty.Id, Ct));
    }

    [Fact]
    public async Task RemoveUserFromDuty_is_forbidden_for_a_caller_holding_EditEvaluation_only_on_another_evaluation()
    {
        var (_, duty, assignee) = await SeedWithAssignedDuty();
        var actor = await Actor().OnNewEvaluation(EvaluationPermission.EditEvaluation).SeedAsync();

        await AssertStatus(HttpStatusCode.Forbidden, await Client(actor).PutAsync($"api/duties/{duty.Id}/users/{assignee.Id}/remove", null, Ct));

        await using var context = NewContext();
        Assert.True(await context.DutyUsers.AnyAsync(x => x.DutyId == duty.Id, Ct));
    }

    [Fact]
    public async Task Delete_removes_the_duty_for_a_caller_holding_EditEvaluation_on_the_evaluation()
    {
        var (graph, duty, _) = await SeedWithAssignedDuty();
        var actor = await Actor().OnEvaluation(graph.Evaluation.Id, permissions: [EvaluationPermission.EditEvaluation]).SeedAsync();

        await AssertStatus(HttpStatusCode.NoContent, await Client(actor).DeleteAsync($"api/duties/{duty.Id}", Ct));

        await using var context = NewContext();
        Assert.False(await context.Duties.AnyAsync(x => x.Id == duty.Id, Ct));
    }

    [Fact]
    public async Task Delete_is_forbidden_for_a_caller_holding_EditEvaluation_only_on_another_evaluation()
    {
        var (_, duty, _) = await SeedWithAssignedDuty();
        var actor = await Actor().OnNewEvaluation(EvaluationPermission.EditEvaluation).SeedAsync();

        await AssertStatus(HttpStatusCode.Forbidden, await Client(actor).DeleteAsync($"api/duties/{duty.Id}", Ct));

        await using var context = NewContext();
        Assert.True(await context.Duties.AnyAsync(x => x.Id == duty.Id, Ct));
    }

    private Task<EvaluationGraph> Seed() => TestScenario.SeedEvaluationAsync(Db, Ct);

    private async Task<(EvaluationGraph Graph, DutyEntity Duty, UserEntity Assignee)> SeedWithAssignedDuty()
    {
        var graph = await Seed();
        var duty = TestData.Duty(graph.Evaluation.Id, graph.Team.Id);
        var assignee = TestData.User(name: "Assignee");
        await Seed(duty, assignee, TestData.DutyUser(duty.Id, assignee.Id));

        return (graph, duty, assignee);
    }
}
