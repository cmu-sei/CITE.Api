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
/// <c>MoveController</c>: an evaluation's moves, read with a view or observe permission (or as a
/// participant), and changed with EditEvaluation.
/// </summary>
public class MoveControllerTests(DatabaseFixture fixture, CiteAppFactory factory) : ApiTestBase(fixture, factory)
{
    /// <summary>A request with no identity is answered with a 401; the anonymous client is the case under test.</summary>
    [Fact]
    public async Task GetByEvaluation_without_an_identity_is_unauthorized()
    {
        await AssertStatus(HttpStatusCode.Unauthorized, await Client().GetAsync($"api/evaluations/{Guid.NewGuid()}/moves", Ct));
    }

    [Fact]
    public async Task GetByEvaluation_lists_the_moves_to_a_caller_holding_ObserveEvaluation_on_it()
    {
        var graph = await Seed();
        var actor = await Actor().OnEvaluation(graph.Evaluation.Id, permissions: [EvaluationPermission.ObserveEvaluation]).SeedAsync();

        var moves = await ReadAsync<List<Move>>(await Client(actor).GetAsync($"api/evaluations/{graph.Evaluation.Id}/moves", Ct));

        Assert.Equal(graph.Move.Id, Assert.Single(moves).Id);
    }

    [Fact]
    public async Task GetByEvaluation_lists_the_moves_to_a_member_of_one_of_its_teams()
    {
        var graph = await Seed();
        var actor = await Actor().OnTeam(graph.Team.Id).SeedAsync();

        var moves = await ReadAsync<List<Move>>(await Client(actor).GetAsync($"api/evaluations/{graph.Evaluation.Id}/moves", Ct));

        Assert.Single(moves);
    }

    [Fact]
    public async Task GetByEvaluation_is_forbidden_while_none_of_its_teams_has_a_member_for_a_caller_holding_ObserveEvaluation_only_on_another_evaluation()
    {
        var graph = await Seed();
        var actor = await Actor().OnNewEvaluation(EvaluationPermission.ObserveEvaluation).SeedAsync();

        await AssertApiError(HttpStatusCode.Forbidden, await Client(actor).GetAsync($"api/evaluations/{graph.Evaluation.Id}/moves", Ct));
    }

    /// <summary>Once anyone is on one of its teams, the moves are listed to a caller with no standing in the evaluation.</summary>
    [Fact]
    public async Task GetByEvaluation_lists_the_moves_to_a_caller_holding_ObserveEvaluation_only_on_another_evaluation_once_anyone_is_on_one_of_its_teams()
    {
        var graph = await Seed();
        var member = TestData.User();
        await Seed(member, TestData.TeamMembership(graph.Team.Id, member.Id));
        var actor = await Actor().OnNewEvaluation(EvaluationPermission.ObserveEvaluation).SeedAsync();

        var moves = await ReadAsync<List<Move>>(await Client(actor).GetAsync($"api/evaluations/{graph.Evaluation.Id}/moves", Ct));

        Assert.Equal(graph.Move.Id, Assert.Single(moves).Id);
    }

    [Fact]
    public async Task Get_returns_the_move_to_a_caller_holding_ObserveEvaluations()
    {
        var graph = await Seed();
        var actor = await Actor().WithSystemPermissions(SystemPermission.ObserveEvaluations).SeedAsync();

        var move = await ReadAsync<Move>(await Client(actor).GetAsync($"api/moves/{graph.Move.Id}", Ct));

        Assert.Equal(graph.Evaluation.Id, move.EvaluationId);
    }

    [Fact]
    public async Task Get_is_forbidden_for_a_team_member_while_the_move_is_ahead_of_the_current_one()
    {
        var graph = await Seed();
        var future = TestData.Move(graph.Evaluation.Id, 1);
        await Seed(future);
        var actor = await Actor().OnTeam(graph.Team.Id).SeedAsync();

        await AssertStatus(HttpStatusCode.Forbidden, await Client(actor).GetAsync($"api/moves/{future.Id}", Ct));
    }

    [Fact]
    public async Task Get_is_forbidden_while_none_of_its_teams_has_a_member_for_a_caller_holding_only_ExecuteEvaluations()
    {
        var graph = await Seed();
        var actor = await Actor().WithSystemPermissions(SystemPermission.ExecuteEvaluations).SeedAsync();

        await AssertStatus(HttpStatusCode.Forbidden, await Client(actor).GetAsync($"api/moves/{graph.Move.Id}", Ct));
    }

    // Same case as GetByEvaluation_lists_the_moves_to_a_caller_holding_ObserveEvaluation_only_on_another_evaluation_once_anyone_is_on_one_of_its_teams.
    [Fact]
    public async Task Get_returns_the_move_to_a_caller_holding_only_ExecuteEvaluations_once_anyone_is_on_one_of_its_teams()
    {
        var graph = await Seed();
        var member = TestData.User();
        await Seed(member, TestData.TeamMembership(graph.Team.Id, member.Id));
        var actor = await Actor().WithSystemPermissions(SystemPermission.ExecuteEvaluations).SeedAsync();

        await AssertStatus(HttpStatusCode.OK, await Client(actor).GetAsync($"api/moves/{graph.Move.Id}", Ct));
    }

    /// <summary>An unknown move id is answered with a 500.</summary>
    [Fact]
    public async Task Get_answers_an_unknown_move_with_a_server_error()
    {
        var error = await AssertApiError(HttpStatusCode.InternalServerError, await RootClient.GetAsync($"api/moves/{Guid.NewGuid()}", Ct));
        Assert.Equal("Sequence contains no elements.", error.Detail);
    }

    [Fact]
    public async Task Create_persists_the_move_and_its_submissions_for_a_caller_holding_EditEvaluation_on_the_evaluation()
    {
        var graph = await Seed();
        await Seed(TestData.TeamMembership(graph.Team.Id, Root.Id));
        var actor = await Actor().OnEvaluation(graph.Evaluation.Id, permissions: [EvaluationPermission.EditEvaluation]).SeedAsync();
        var id = Guid.NewGuid();

        var response = await Client(actor).PostAsJsonAsync("api/moves", NewMove(id, graph.Evaluation.Id, 1), Ct);

        await AssertStatus(HttpStatusCode.Created, response);
        await using var context = NewContext();
        Assert.Equal("New Move", (await context.Moves.SingleAsync(x => x.Id == id, Ct)).Description);
        var submissions = await context.Submissions.Where(x => x.EvaluationId == graph.Evaluation.Id && x.MoveNumber == 1).ToListAsync(Ct);
        Assert.Equal(3, submissions.Count);
    }

    [Fact]
    public async Task Create_broadcasts_the_move_to_the_evaluation_group()
    {
        var graph = await Seed();
        var id = Guid.NewGuid();

        await AssertStatus(HttpStatusCode.Created, await RootClient.PostAsJsonAsync("api/moves", NewMove(id, graph.Evaluation.Id, 1), Ct));

        var broadcast = Assert.Single(Factory.Hub<MainHub>().ToGroup(graph.Evaluation.Id), x => x.Method == MainHubMethods.MoveCreated);
        Assert.Equal(id, Assert.IsType<Move>(broadcast.Arguments[0]).Id);
    }

    [Fact]
    public async Task Create_is_forbidden_for_a_caller_holding_only_ExecuteEvaluation()
    {
        var graph = await Seed();
        var actor = await Actor().OnEvaluation(graph.Evaluation.Id, permissions: [EvaluationPermission.ExecuteEvaluation]).SeedAsync();
        var id = Guid.NewGuid();

        await AssertStatus(HttpStatusCode.Forbidden, await Client(actor).PostAsJsonAsync("api/moves", NewMove(id, graph.Evaluation.Id, 1), Ct));

        await using var context = NewContext();
        Assert.False(await context.Moves.AnyAsync(x => x.Id == id, Ct));
    }

    [Fact]
    public async Task Create_is_forbidden_for_a_caller_holding_EditEvaluation_only_on_another_evaluation()
    {
        var graph = await Seed();
        var actor = await Actor().OnNewEvaluation(EvaluationPermission.EditEvaluation).SeedAsync();

        await AssertStatus(HttpStatusCode.Forbidden, await Client(actor).PostAsJsonAsync("api/moves", NewMove(Guid.NewGuid(), graph.Evaluation.Id, 1), Ct));
    }

    /// <summary>A move number the evaluation already has is answered with a 500.</summary>
    [Fact]
    public async Task Create_answers_a_move_number_that_is_already_taken_with_a_server_error()
    {
        var graph = await Seed();

        var error = await AssertApiError(HttpStatusCode.InternalServerError, await RootClient.PostAsJsonAsync("api/moves", NewMove(Guid.NewGuid(), graph.Evaluation.Id, 0), Ct));
        Assert.StartsWith("An error occurred while saving the entity changes.", error.Detail);
    }

    [Fact]
    public async Task Update_changes_the_move_for_a_caller_holding_EditEvaluation_on_its_evaluation()
    {
        var graph = await Seed();
        var actor = await Actor().OnEvaluation(graph.Evaluation.Id, permissions: [EvaluationPermission.EditEvaluation]).SeedAsync();

        var response = await Client(actor).PutAsJsonAsync($"api/moves/{graph.Move.Id}", NewMove(graph.Move.Id, graph.Evaluation.Id, 0) with { Description = "Changed" }, Ct);

        await AssertStatus(HttpStatusCode.OK, response);
        await using var context = NewContext();
        Assert.Equal("Changed", (await context.Moves.SingleAsync(x => x.Id == graph.Move.Id, Ct)).Description);
    }

    [Fact]
    public async Task Update_is_forbidden_for_a_caller_holding_only_ViewEvaluation()
    {
        var graph = await Seed();
        var actor = await Actor().OnEvaluation(graph.Evaluation.Id, permissions: [EvaluationPermission.ViewEvaluation]).SeedAsync();

        await AssertStatus(HttpStatusCode.Forbidden, await Client(actor).PutAsJsonAsync($"api/moves/{graph.Move.Id}",
            NewMove(graph.Move.Id, graph.Evaluation.Id, 0) with { Description = "Changed" }, Ct));

        await using var context = NewContext();
        Assert.Equal(graph.Move.Description, (await context.Moves.SingleAsync(x => x.Id == graph.Move.Id, Ct)).Description);
    }

    /// <summary>EditEvaluation on the evaluation named in the body lets a caller take a move of another evaluation.</summary>
    [Fact]
    public async Task Update_moves_a_move_of_another_evaluation_into_the_one_the_caller_edits()
    {
        var graph = await Seed();
        var actor = await Actor().OnNewEvaluation(EvaluationPermission.EditEvaluation).SeedAsync();
        var editable = actor.NewEvaluationIds[0];

        var response = await Client(actor).PutAsJsonAsync($"api/moves/{graph.Move.Id}", NewMove(graph.Move.Id, editable, 0), Ct);

        await AssertStatus(HttpStatusCode.OK, response);
        await using var context = NewContext();
        Assert.Equal(editable, (await context.Moves.SingleAsync(x => x.Id == graph.Move.Id, Ct)).EvaluationId);
    }

    [Fact]
    public async Task Delete_removes_the_move_and_its_submissions_for_a_caller_holding_EditEvaluations()
    {
        var graph = await Seed();
        await Seed(TestData.Submission(graph.ScoringModel.Id, graph.Evaluation.Id, graph.Team.Id));
        var actor = await Actor().WithSystemPermissions(SystemPermission.EditEvaluations).SeedAsync();

        await AssertStatus(HttpStatusCode.NoContent, await Client(actor).DeleteAsync($"api/moves/{graph.Move.Id}", Ct));

        await using var context = NewContext();
        Assert.False(await context.Moves.AnyAsync(x => x.Id == graph.Move.Id, Ct));
        Assert.False(await context.Submissions.AnyAsync(x => x.EvaluationId == graph.Evaluation.Id, Ct));
    }

    [Fact]
    public async Task Delete_broadcasts_the_id_to_the_evaluation_group()
    {
        var graph = await Seed();

        await AssertStatus(HttpStatusCode.NoContent, await RootClient.DeleteAsync($"api/moves/{graph.Move.Id}", Ct));

        Assert.Equal(graph.Move.Id, Assert.Single(Factory.Hub<MainHub>().ToGroup(graph.Evaluation.Id), x => x.Method == MainHubMethods.MoveDeleted).Argument);
    }

    /// <summary>EditEvaluation on the move's own evaluation does not let its holder delete the move.</summary>
    [Fact]
    public async Task Delete_is_forbidden_for_a_caller_holding_EditEvaluation_on_the_moves_evaluation()
    {
        var graph = await Seed();
        var actor = await Actor().OnEvaluation(graph.Evaluation.Id, permissions: [EvaluationPermission.EditEvaluation]).SeedAsync();

        await AssertStatus(HttpStatusCode.Forbidden, await Client(actor).DeleteAsync($"api/moves/{graph.Move.Id}", Ct));
    }

    [Fact]
    public async Task Delete_is_forbidden_for_a_caller_holding_only_ExecuteEvaluations()
    {
        var graph = await Seed();
        var actor = await Actor().WithSystemPermissions(SystemPermission.ExecuteEvaluations).SeedAsync();

        await AssertStatus(HttpStatusCode.Forbidden, await Client(actor).DeleteAsync($"api/moves/{graph.Move.Id}", Ct));

        await using var context = NewContext();
        Assert.True(await context.Moves.AnyAsync(x => x.Id == graph.Move.Id, Ct));
    }

    private Task<EvaluationGraph> Seed() => TestScenario.SeedEvaluationAsync(Db, Ct);

    private static MoveBody NewMove(Guid id, Guid evaluationId, int moveNumber) =>
        new(id, "New Move", moveNumber, TestData.DefaultDateCreated, evaluationId);

    private sealed record MoveBody(Guid Id, string Description, int MoveNumber, DateTime SituationTime, Guid EvaluationId);
}
