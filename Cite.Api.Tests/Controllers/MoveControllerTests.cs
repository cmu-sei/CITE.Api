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

    [Theory]
    [InlineData(EvaluationPermission.ObserveEvaluation)]
    [InlineData(EvaluationPermission.ViewEvaluation)]
    [InlineData(EvaluationPermission.ExecuteEvaluation)]
    public async Task GetByEvaluation_lists_the_moves_to_a_caller_holding_an_evaluation_permission_on_it(EvaluationPermission permission)
    {
        var graph = await Seed();
        var actor = await Actor().OnEvaluation(graph.Evaluation.Id, permissions: [permission]).SeedAsync();

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

    [Theory]
    [InlineData(SystemPermission.ViewEvaluations)]
    [InlineData(SystemPermission.ObserveEvaluations)]
    [InlineData(SystemPermission.ExecuteEvaluations)]
    public async Task GetByEvaluation_lists_the_moves_to_a_caller_holding_a_system_permission(SystemPermission permission)
    {
        var graph = await Seed();
        var actor = await Actor().WithSystemPermissions(permission).SeedAsync();

        var moves = await ReadAsync<List<Move>>(await Client(actor).GetAsync($"api/evaluations/{graph.Evaluation.Id}/moves", Ct));

        Assert.Equal(graph.Move.Id, Assert.Single(moves).Id);
    }

    [Fact]
    public async Task GetByEvaluation_is_forbidden_for_a_caller_holding_ObserveEvaluation_only_on_another_evaluation_when_others_are_on_its_teams()
    {
        var graph = await Seed();
        var member = TestData.User();
        await Seed(member, TestData.TeamMembership(graph.Team.Id, member.Id));
        var actor = await Actor().OnNewEvaluation(EvaluationPermission.ObserveEvaluation).SeedAsync();

        await AssertApiError(HttpStatusCode.Forbidden, await Client(actor).GetAsync($"api/evaluations/{graph.Evaluation.Id}/moves", Ct));
    }

    [Fact]
    public async Task GetByEvaluation_lists_only_the_moves_up_to_the_current_one_to_a_member_of_one_of_its_teams()
    {
        var graph = await Seed();
        var future = TestData.Move(graph.Evaluation.Id, 1);
        await Seed(future);
        var actor = await Actor().OnTeam(graph.Team.Id).SeedAsync();

        var moves = await ReadAsync<List<Move>>(await Client(actor).GetAsync($"api/evaluations/{graph.Evaluation.Id}/moves", Ct));

        Assert.Equal(graph.Move.Id, Assert.Single(moves).Id);
    }

    /// <summary>Advancing moves (ExecuteEvaluation) needs the moves ahead of the current one, as viewing and observing do.</summary>
    [Theory]
    [InlineData(EvaluationPermission.ViewEvaluation)]
    [InlineData(EvaluationPermission.ObserveEvaluation)]
    [InlineData(EvaluationPermission.ExecuteEvaluation)]
    public async Task GetByEvaluation_lists_the_future_moves_to_a_caller_holding_an_evaluation_permission_on_it(EvaluationPermission permission)
    {
        var graph = await Seed();
        await Seed(TestData.Move(graph.Evaluation.Id, 1));
        var actor = await Actor().OnEvaluation(graph.Evaluation.Id, permissions: [permission]).SeedAsync();

        Assert.Equal(2, (await ReadAsync<List<Move>>(await Client(actor).GetAsync($"api/evaluations/{graph.Evaluation.Id}/moves", Ct))).Count);
    }

    [Theory]
    [InlineData(SystemPermission.ViewEvaluations)]
    [InlineData(SystemPermission.ObserveEvaluations)]
    [InlineData(SystemPermission.ExecuteEvaluations)]
    public async Task Get_returns_the_move_to_a_caller_holding_a_system_permission(SystemPermission permission)
    {
        var graph = await Seed();
        var actor = await Actor().WithSystemPermissions(permission).SeedAsync();

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

    [Theory]
    [InlineData(EvaluationPermission.ViewEvaluation)]
    [InlineData(EvaluationPermission.ObserveEvaluation)]
    [InlineData(EvaluationPermission.ExecuteEvaluation)]
    public async Task Get_returns_a_future_move_to_a_caller_holding_an_evaluation_permission_on_it(EvaluationPermission permission)
    {
        var graph = await Seed();
        var future = TestData.Move(graph.Evaluation.Id, 1);
        await Seed(future);
        var actor = await Actor().OnEvaluation(graph.Evaluation.Id, permissions: [permission]).SeedAsync();

        await AssertStatus(HttpStatusCode.OK, await Client(actor).GetAsync($"api/moves/{future.Id}", Ct));
    }

    [Fact]
    public async Task Get_returns_the_current_move_to_a_member_of_one_of_its_teams()
    {
        var graph = await Seed();
        var actor = await Actor().OnTeam(graph.Team.Id).SeedAsync();

        await AssertStatus(HttpStatusCode.OK, await Client(actor).GetAsync($"api/moves/{graph.Move.Id}", Ct));
    }

    [Fact]
    public async Task Get_answers_an_unknown_move_with_not_found()
    {
        await AssertApiError(HttpStatusCode.NotFound, await RootClient.GetAsync($"api/moves/{Guid.NewGuid()}", Ct));
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
    public async Task Create_broadcasts_a_move_up_to_the_current_one_to_the_evaluation_group()
    {
        var graph = await Seed();
        graph.Evaluation.CurrentMoveNumber = 1;
        await Db.SaveChangesAsync(Ct);
        var id = Guid.NewGuid();

        await AssertStatus(HttpStatusCode.Created, await RootClient.PostAsJsonAsync("api/moves", NewMove(id, graph.Evaluation.Id, 1), Ct));

        var broadcast = Assert.Single(Factory.Hub<MainHub>().ToGroup(graph.Evaluation.Id), x => x.Method == MainHubMethods.MoveCreated);
        Assert.Equal(id, Assert.IsType<Move>(broadcast.Arguments[0]).Id);
    }

    /// <summary>A future move reaches only the admin group; teams get it when it becomes current.</summary>
    [Fact]
    public async Task Create_does_not_broadcast_a_future_move_to_the_evaluation_group()
    {
        var graph = await Seed();
        var id = Guid.NewGuid();

        await AssertStatus(HttpStatusCode.Created, await RootClient.PostAsJsonAsync("api/moves", NewMove(id, graph.Evaluation.Id, 1), Ct));

        Assert.DoesNotContain(Factory.Hub<MainHub>().ToGroup(graph.Evaluation.Id), x => x.Method == MainHubMethods.MoveCreated);
        // the admin group is shared by every test's evaluation, so look for this move only
        Assert.Single(Factory.Hub<MainHub>().ToGroup(MainHub.ADMIN_DATA_GROUP), x => x.Method == MainHubMethods.MoveCreated && ((Move)x.Arguments[0]).Id == id);
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

    [Fact]
    public async Task Create_persists_the_move_for_a_caller_holding_EditEvaluations()
    {
        var graph = await Seed();
        var actor = await Actor().WithSystemPermissions(SystemPermission.EditEvaluations).SeedAsync();
        var id = Guid.NewGuid();

        await AssertStatus(HttpStatusCode.Created, await Client(actor).PostAsJsonAsync("api/moves", NewMove(id, graph.Evaluation.Id, 1), Ct));

        await using var context = NewContext();
        Assert.Equal("New Move", (await context.Moves.SingleAsync(x => x.Id == id, Ct)).Description);
    }

    [Fact]
    public async Task Create_is_forbidden_for_a_caller_holding_only_ViewEvaluations()
    {
        var graph = await Seed();
        var actor = await Actor().WithSystemPermissions(SystemPermission.ViewEvaluations).SeedAsync();
        var id = Guid.NewGuid();

        await AssertApiError(HttpStatusCode.Forbidden, await Client(actor).PostAsJsonAsync("api/moves", NewMove(id, graph.Evaluation.Id, 1), Ct));

        await using var context = NewContext();
        Assert.False(await context.Moves.AnyAsync(x => x.Id == id, Ct));
    }

    [Fact]
    public async Task Create_answers_a_move_number_that_is_already_taken_with_conflict()
    {
        var graph = await Seed();

        await AssertApiError(HttpStatusCode.Conflict, await RootClient.PostAsJsonAsync("api/moves", NewMove(Guid.NewGuid(), graph.Evaluation.Id, 0), Ct));
    }

    [Fact]
    public async Task Update_answers_a_move_number_that_is_already_taken_with_conflict()
    {
        var graph = await Seed();
        var second = TestData.Move(graph.Evaluation.Id, 1);
        await Seed(second);

        await AssertApiError(HttpStatusCode.Conflict, await RootClient.PutAsJsonAsync($"api/moves/{second.Id}", NewMove(second.Id, graph.Evaluation.Id, 0), Ct));

        await using var context = NewContext();
        Assert.Equal(1, (await context.Moves.SingleAsync(x => x.Id == second.Id, Ct)).MoveNumber);
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

    [Fact]
    public async Task Update_changes_the_move_for_a_caller_holding_EditEvaluations()
    {
        var graph = await Seed();
        var actor = await Actor().WithSystemPermissions(SystemPermission.EditEvaluations).SeedAsync();

        await AssertStatus(HttpStatusCode.OK, await Client(actor).PutAsJsonAsync($"api/moves/{graph.Move.Id}",
            NewMove(graph.Move.Id, graph.Evaluation.Id, 0) with { Description = "Changed" }, Ct));

        await using var context = NewContext();
        Assert.Equal("Changed", (await context.Moves.SingleAsync(x => x.Id == graph.Move.Id, Ct)).Description);
    }

    [Fact]
    public async Task Update_is_forbidden_for_a_caller_holding_only_ViewEvaluations()
    {
        var graph = await Seed();
        var actor = await Actor().WithSystemPermissions(SystemPermission.ViewEvaluations).SeedAsync();

        await AssertApiError(HttpStatusCode.Forbidden, await Client(actor).PutAsJsonAsync($"api/moves/{graph.Move.Id}",
            NewMove(graph.Move.Id, graph.Evaluation.Id, 0) with { Description = "Changed" }, Ct));

        await using var context = NewContext();
        Assert.Equal(graph.Move.Description, (await context.Moves.SingleAsync(x => x.Id == graph.Move.Id, Ct)).Description);
    }

    [Fact]
    public async Task Update_is_forbidden_for_a_caller_holding_EditEvaluation_only_on_another_evaluation_named_in_the_body()
    {
        var graph = await Seed();
        var actor = await Actor().OnNewEvaluation(EvaluationPermission.EditEvaluation).SeedAsync();
        var editable = actor.NewEvaluationIds[0];

        await AssertApiError(HttpStatusCode.Forbidden, await Client(actor).PutAsJsonAsync($"api/moves/{graph.Move.Id}", NewMove(graph.Move.Id, editable, 0), Ct));

        await using var context = NewContext();
        Assert.Equal(graph.Evaluation.Id, (await context.Moves.SingleAsync(x => x.Id == graph.Move.Id, Ct)).EvaluationId);
    }

    [Fact]
    public async Task Update_keeps_the_moves_evaluation_whatever_the_body_sends()
    {
        var graph = await Seed();
        var other = await TestScenario.SeedEvaluationAsync(Db, Ct, "Other");
        var actor = await Actor().OnEvaluation(graph.Evaluation.Id, permissions: [EvaluationPermission.EditEvaluation]).SeedAsync();

        await AssertStatus(HttpStatusCode.OK, await Client(actor).PutAsJsonAsync($"api/moves/{graph.Move.Id}", NewMove(graph.Move.Id, other.Evaluation.Id, 0) with { Description = "Changed" }, Ct));

        await using var context = NewContext();
        var stored = await context.Moves.SingleAsync(x => x.Id == graph.Move.Id, Ct);
        Assert.Equal(("Changed", graph.Evaluation.Id), (stored.Description, stored.EvaluationId));
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

    [Fact]
    public async Task Delete_removes_the_move_for_a_caller_holding_EditEvaluation_on_the_moves_evaluation()
    {
        var graph = await Seed();
        var actor = await Actor().OnEvaluation(graph.Evaluation.Id, permissions: [EvaluationPermission.EditEvaluation]).SeedAsync();

        await AssertStatus(HttpStatusCode.NoContent, await Client(actor).DeleteAsync($"api/moves/{graph.Move.Id}", Ct));

        await using var context = NewContext();
        Assert.False(await context.Moves.AnyAsync(x => x.Id == graph.Move.Id, Ct));
    }

    /// <summary>The caller edits an evaluation created with the id of a move in another evaluation.</summary>
    [Fact]
    public async Task Delete_is_forbidden_for_a_caller_holding_EditEvaluation_on_an_evaluation_whose_id_is_the_moves_id()
    {
        var graph = await Seed();
        await TestScenario.SeedSubmissionAsync(Db, Ct, graph, graph.Team.Id, moveNumber: graph.Move.MoveNumber);
        var lookalike = TestData.Evaluation(graph.ScoringModel.Id, "Lookalike");
        lookalike.Id = graph.Move.Id;
        await Seed(lookalike);
        var actor = await Actor().OnEvaluation(lookalike.Id, permissions: [EvaluationPermission.EditEvaluation]).SeedAsync();

        await AssertApiError(HttpStatusCode.Forbidden, await Client(actor).DeleteAsync($"api/moves/{graph.Move.Id}", Ct));

        await using var context = NewContext();
        Assert.True(await context.Moves.AnyAsync(x => x.Id == graph.Move.Id, Ct));
        Assert.True(await context.Submissions.AnyAsync(x => x.EvaluationId == graph.Evaluation.Id, Ct));
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

    /// <summary>A move created with its evaluation's id is deleted for a caller holding EditEvaluation on that evaluation.</summary>
    [Fact]
    public async Task Delete_removes_a_move_whose_id_is_its_evaluations_id_for_a_caller_holding_EditEvaluation_there()
    {
        var (graph, move) = await SeedWithMoveIdOfItsEvaluation();
        var actor = await Actor().OnEvaluation(graph.Evaluation.Id, permissions: [EvaluationPermission.EditEvaluation]).SeedAsync();

        await AssertStatus(HttpStatusCode.NoContent, await Client(actor).DeleteAsync($"api/moves/{move.Id}", Ct));

        await using var context = NewContext();
        Assert.False(await context.Moves.AnyAsync(x => x.Id == move.Id, Ct));
    }

    [Fact]
    public async Task Delete_is_forbidden_on_a_move_whose_id_is_its_evaluations_id_for_a_caller_holding_only_ExecuteEvaluation_there()
    {
        var (graph, move) = await SeedWithMoveIdOfItsEvaluation();
        var actor = await Actor().OnEvaluation(graph.Evaluation.Id, permissions: [EvaluationPermission.ExecuteEvaluation]).SeedAsync();

        await AssertApiError(HttpStatusCode.Forbidden, await Client(actor).DeleteAsync($"api/moves/{move.Id}", Ct));

        await using var context = NewContext();
        Assert.True(await context.Moves.AnyAsync(x => x.Id == move.Id, Ct));
    }

    private Task<EvaluationGraph> Seed() => TestScenario.SeedEvaluationAsync(Db, Ct);

    /// <summary>A second move of a new evaluation, whose row carries the evaluation's own id.</summary>
    private async Task<(EvaluationGraph Graph, MoveEntity Move)> SeedWithMoveIdOfItsEvaluation()
    {
        var graph = await Seed();
        var move = TestData.Move(graph.Evaluation.Id, 1);
        move.Id = graph.Evaluation.Id;
        await Seed(move);

        return (graph, move);
    }

    private static MoveBody NewMove(Guid id, Guid evaluationId, int moveNumber) =>
        new(id, "New Move", moveNumber, TestData.DefaultDateCreated, evaluationId);

    private sealed record MoveBody(Guid Id, string Description, int MoveNumber, DateTime SituationTime, Guid EvaluationId);
}
