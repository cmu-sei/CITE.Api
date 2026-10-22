// Copyright 2026 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.

using System.Net;
using System.Net.Http.Json;
using Cite.Api.Data.Enumerations;
using Cite.Api.Tests.Support;
using Microsoft.EntityFrameworkCore;

namespace Cite.Api.Tests.Controllers;

public class ParentIdUpdateTests(DatabaseFixture fixture, CiteAppFactory factory) : ApiTestBase(fixture, factory)
{
    [Fact]
    public async Task Update_moves_an_action_into_another_evaluation_for_a_caller_editing_only_the_body_evaluation()
    {
        var source = await Graph("Source");
        var destination = await Graph("Destination");
        var action = TestData.Action(source.Evaluation.Id, source.Team.Id);
        await Seed(action);
        var actor = await Actor().OnEvaluation(destination.Evaluation.Id, permissions: [EvaluationPermission.EditEvaluation]).SeedAsync();

        var response = await Client(actor).PutAsJsonAsync($"api/actions/{action.Id}", new { id = action.Id, evaluationId = destination.Evaluation.Id, teamId = source.Team.Id, description = action.Description }, Ct);

        await AssertStatus(HttpStatusCode.OK, response);
        Assert.Equal(destination.Evaluation.Id, await ReadBack(db => db.Actions.Where(x => x.Id == action.Id).Select(x => x.EvaluationId).SingleAsync(Ct)));
    }

    [Fact]
    public async Task Update_replaces_an_evaluations_scoring_model_for_a_caller_editing_only_the_evaluation()
    {
        var graph = await Graph("Source");
        var other = await Graph("Other");
        var actor = await Actor().OnEvaluation(graph.Evaluation.Id, permissions: [EvaluationPermission.EditEvaluation]).SeedAsync();

        var response = await Client(actor).PutAsJsonAsync($"api/evaluations/{graph.Evaluation.Id}", new { id = graph.Evaluation.Id, description = graph.Evaluation.Description, status = "Active", situationTime = TestData.DefaultDateCreated, scoringModelId = other.ScoringModel.Id }, Ct);

        await AssertStatus(HttpStatusCode.OK, response);
        Assert.Equal(other.ScoringModel.Id, await ReadBack(db => db.Evaluations.Where(x => x.Id == graph.Evaluation.Id).Select(x => x.ScoringModelId).SingleAsync(Ct)));
    }

    [Fact]
    public async Task Update_attaches_a_scoring_model_to_an_evaluation_the_caller_does_not_manage()
    {
        var graph = await Graph("Other");
        var model = TestData.ScoringModel();
        await Seed(model);
        var actor = await Actor().OnScoringModel(model.Id, permissions: [ScoringModelPermission.EditScoringModel]).SeedAsync();

        var response = await Client(actor).PutAsJsonAsync($"api/scoringModels/{model.Id}", new { id = model.Id, description = model.Description, status = "Active", evaluationId = graph.Evaluation.Id }, Ct);

        await AssertStatus(HttpStatusCode.OK, response);
        Assert.Equal(graph.Evaluation.Id, await ReadBack(db => db.ScoringModels.Where(x => x.Id == model.Id).Select(x => x.EvaluationId).SingleAsync(Ct)));
    }

    [Fact]
    public async Task Update_moves_a_scoring_option_into_a_category_of_a_model_the_caller_cannot_edit()
    {
        var source = await Graph("Source");
        var destination = await Graph("Destination");
        var actor = await Actor().OnScoringModel(source.ScoringModel.Id, permissions: [ScoringModelPermission.EditScoringModel]).SeedAsync();

        var response = await Client(actor).PutAsJsonAsync($"api/scoringOptions/{source.Option.Id}", new { id = source.Option.Id, description = source.Option.Description, scoringCategoryId = destination.Category.Id }, Ct);

        await AssertStatus(HttpStatusCode.OK, response);
        Assert.Equal(destination.Category.Id, await ReadBack(db => db.ScoringOptions.Where(x => x.Id == source.Option.Id).Select(x => x.ScoringCategoryId).SingleAsync(Ct)));
    }

    [Fact]
    public async Task Update_repoints_a_submission_category_to_a_scoring_category_the_caller_cannot_edit()
    {
        var source = await Graph("Source");
        var destination = await Graph("Destination");
        var submission = await TestScenario.SeedSubmissionAsync(Db, Ct, source, source.Team.Id);
        var actor = await Actor().OnEvaluation(source.Evaluation.Id, permissions: [EvaluationPermission.EditEvaluation]).SeedAsync();

        var response = await Client(actor).PutAsJsonAsync($"api/submissionCategories/{submission.Category.Id}", new { id = submission.Category.Id, submissionId = submission.Submission.Id, scoringCategoryId = destination.Category.Id }, Ct);

        await AssertStatus(HttpStatusCode.OK, response);
        Assert.Equal(destination.Category.Id, await ReadBack(db => db.SubmissionCategories.Where(x => x.Id == submission.Category.Id).Select(x => x.ScoringCategoryId).SingleAsync(Ct)));
    }

    [Fact]
    public async Task Update_moves_a_submission_category_to_a_submission_of_another_evaluation()
    {
        var source = await Graph("Source");
        var destination = await Graph("Destination");
        var mine = await TestScenario.SeedSubmissionAsync(Db, Ct, source, source.Team.Id);
        var theirs = await TestScenario.SeedSubmissionAsync(Db, Ct, destination, destination.Team.Id);
        var actor = await Actor().OnEvaluation(source.Evaluation.Id, permissions: [EvaluationPermission.EditEvaluation]).SeedAsync();

        var response = await Client(actor).PutAsJsonAsync($"api/submissionCategories/{mine.Category.Id}", new { id = mine.Category.Id, submissionId = theirs.Submission.Id, scoringCategoryId = source.Category.Id }, Ct);

        await AssertStatus(HttpStatusCode.OK, response);
        Assert.Equal(theirs.Submission.Id, await ReadBack(db => db.SubmissionCategories.Where(x => x.Id == mine.Category.Id).Select(x => x.SubmissionId).SingleAsync(Ct)));
    }

    [Theory]
    [InlineData("api/submissionComments/{0}")]
    [InlineData("api/submissions/{1}/comments/{0}")]
    public async Task Update_moves_a_comment_to_an_option_of_another_evaluation(string route)
    {
        var source = await Graph("Source");
        var destination = await Graph("Destination");
        var mine = await TestScenario.SeedSubmissionAsync(Db, Ct, source, source.Team.Id);
        var theirs = await TestScenario.SeedSubmissionAsync(Db, Ct, destination, destination.Team.Id);
        var comment = TestData.SubmissionComment(mine.Option.Id);
        await Seed(comment);
        var actor = await Actor().OnTeam(source.Team.Id, permissions: [TeamPermission.ViewTeam, TeamPermission.EditTeamScore]).SeedAsync();

        var response = await Client(actor).PutAsJsonAsync(string.Format(route, comment.Id, mine.Submission.Id), new { id = comment.Id, submissionOptionId = theirs.Option.Id, comment = comment.Comment }, Ct);

        await AssertStatus(HttpStatusCode.OK, response);
        Assert.Equal(theirs.Option.Id, await ReadBack(db => db.SubmissionComments.Where(x => x.Id == comment.Id).Select(x => x.SubmissionOptionId).SingleAsync(Ct)));
    }

    [Fact]
    public async Task Update_repoints_a_submission_option_to_another_models_scoring_option()
    {
        var source = await Graph("Source");
        var destination = await Graph("Destination");
        var submission = await TestScenario.SeedSubmissionAsync(Db, Ct, source, source.Team.Id);
        var actor = await Actor().OnEvaluation(source.Evaluation.Id, permissions: [EvaluationPermission.ManageEvaluation]).SeedAsync();

        var response = await Client(actor).PutAsJsonAsync($"api/submissionOptions/{submission.Option.Id}", new { id = submission.Option.Id, submissionCategoryId = submission.Category.Id, scoringOptionId = destination.Option.Id }, Ct);

        await AssertStatus(HttpStatusCode.OK, response);
        Assert.Equal(destination.Option.Id, await ReadBack(db => db.SubmissionOptions.Where(x => x.Id == submission.Option.Id).Select(x => x.ScoringOptionId).SingleAsync(Ct)));
    }

    [Fact]
    public async Task Update_moves_a_submission_and_changes_its_model_team_and_user_without_destination_permissions()
    {
        var source = await Graph("Source");
        var destination = await Graph("Destination");
        var submission = await TestScenario.SeedSubmissionAsync(Db, Ct, source, source.Team.Id);
        var user = TestData.User();
        await Seed(user);
        var actor = await Actor().OnTeam(source.Team.Id, permissions: [TeamPermission.SubmitTeamScore]).SeedAsync();

        var response = await Client(actor).PutAsJsonAsync($"api/submissions/{submission.Submission.Id}", new { id = submission.Submission.Id, evaluationId = destination.Evaluation.Id, scoringModelId = destination.ScoringModel.Id, teamId = destination.Team.Id, userId = user.Id, moveNumber = 0, status = "Active" }, Ct);

        await AssertStatus(HttpStatusCode.OK, response);
        var stored = await ReadBack(db => db.Submissions.SingleAsync(x => x.Id == submission.Submission.Id, Ct));
        Assert.Equal(destination.Evaluation.Id, stored.EvaluationId);
        Assert.Equal(destination.ScoringModel.Id, stored.ScoringModelId);
        Assert.Equal(destination.Team.Id, stored.TeamId);
        Assert.Equal(user.Id, stored.UserId);
    }

    [Fact]
    public async Task Update_answers_an_existing_move_number_with_a_server_error_and_keeps_the_original_number()
    {
        var graph = await Graph("Source");
        var move = TestData.Move(graph.Evaluation.Id, 1);
        await Seed(move);

        var response = await RootClient.PutAsJsonAsync($"api/moves/{move.Id}", new { id = move.Id, evaluationId = graph.Evaluation.Id, moveNumber = 0, description = move.Description, situationTime = TestData.DefaultDateCreated }, Ct);

        var error = await AssertApiError(HttpStatusCode.InternalServerError, response);
        Assert.Equal("An error occurred while saving the entity changes. See the inner exception for details.", error.Detail);
        Assert.Equal(1, await ReadBack(db => db.Moves.Where(x => x.Id == move.Id).Select(x => x.MoveNumber).SingleAsync(Ct)));
    }

    [Fact]
    public async Task Update_answers_an_existing_submission_scope_with_a_server_error_and_keeps_the_original_move()
    {
        var graph = await Graph("Source");
        var original = await TestScenario.SeedSubmissionAsync(Db, Ct, graph, graph.Team.Id, moveNumber: 1);
        await TestScenario.SeedSubmissionAsync(Db, Ct, graph, graph.Team.Id);
        var actor = await Actor().OnTeam(graph.Team.Id, permissions: [TeamPermission.SubmitTeamScore]).SeedAsync();

        var response = await Client(actor).PutAsJsonAsync($"api/submissions/{original.Submission.Id}", new { id = original.Submission.Id, evaluationId = graph.Evaluation.Id, scoringModelId = graph.ScoringModel.Id, teamId = graph.Team.Id, moveNumber = 0, status = "Active" }, Ct);

        var error = await AssertApiError(HttpStatusCode.InternalServerError, response);
        Assert.Equal("An error occurred while saving the entity changes. See the inner exception for details.", error.Detail);
        Assert.Equal(1, await ReadBack(db => db.Submissions.Where(x => x.Id == original.Submission.Id).Select(x => x.MoveNumber).SingleAsync(Ct)));
    }

    [Fact]
    public async Task Update_answers_an_existing_role_name_with_a_server_error_and_keeps_the_original_name()
    {
        var role = TestData.SystemRole("Original Role");
        await Seed(role);

        var response = await RootClient.PutAsJsonAsync($"api/system-roles/{role.Id}", new { id = role.Id, name = "Observer", permissions = Array.Empty<string>() }, Ct);

        var error = await AssertApiError(HttpStatusCode.InternalServerError, response);
        Assert.Equal("An error occurred while saving the entity changes. See the inner exception for details.", error.Detail);
        Assert.Equal("Original Role", await ReadBack(db => db.SystemRoles.Where(x => x.Id == role.Id).Select(x => x.Name).SingleAsync(Ct)));
    }

    [Fact]
    public async Task Update_overwrites_a_nested_evaluation_for_a_caller_managing_only_the_actions_team()
    {
        var source = await Graph("Source");
        var destination = await Graph("Destination");
        var action = TestData.Action(source.Evaluation.Id, source.Team.Id);
        await Seed(action);
        var actor = await Actor().OnTeam(source.Team.Id, permissions: [TeamPermission.ManageTeam]).SeedAsync();

        var response = await Client(actor).PutAsJsonAsync($"api/actions/{action.Id}", new
        {
            id = action.Id,
            evaluationId = source.Evaluation.Id,
            teamId = source.Team.Id,
            description = action.Description,
            evaluation = new { id = destination.Evaluation.Id, description = "Overwritten", status = "Active", scoringModelId = destination.ScoringModel.Id, situationTime = TestData.DefaultDateCreated }
        }, Ct);

        await AssertStatus(HttpStatusCode.OK, response);
        Assert.Equal("Overwritten", await ReadBack(db => db.Evaluations.Where(x => x.Id == destination.Evaluation.Id).Select(x => x.Description).SingleAsync(Ct)));
        Assert.Equal(destination.Evaluation.Id, await ReadBack(db => db.Actions.Where(x => x.Id == action.Id).Select(x => x.EvaluationId).SingleAsync(Ct)));
    }

    [Fact]
    public async Task AddComment_stores_a_comment_under_another_evaluations_option_while_authorizing_the_route_submission()
    {
        var source = await Graph("Source");
        var destination = await Graph("Destination");
        var mine = await TestScenario.SeedSubmissionAsync(Db, Ct, source, source.Team.Id);
        var theirs = await TestScenario.SeedSubmissionAsync(Db, Ct, destination, destination.Team.Id);
        var actor = await Actor().OnTeam(source.Team.Id, permissions: [TeamPermission.EditTeamScore]).SeedAsync();
        var id = Guid.NewGuid();

        var response = await Client(actor).PostAsJsonAsync($"api/submissions/{mine.Submission.Id}/comments", new { id, submissionOptionId = theirs.Option.Id, comment = "Added elsewhere" }, Ct);

        await AssertStatus(HttpStatusCode.OK, response);
        Assert.Equal(theirs.Option.Id, await ReadBack(db => db.SubmissionComments.Where(x => x.Id == id).Select(x => x.SubmissionOptionId).SingleAsync(Ct)));
        Assert.Equal("Added elsewhere", await ReadBack(db => db.SubmissionComments.Where(x => x.Id == id).Select(x => x.Comment).SingleAsync(Ct)));
    }

    private Task<EvaluationGraph> Graph(string description) => TestScenario.SeedEvaluationAsync(Db, Ct, description);
}
