// Copyright 2026 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.

using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Cite.Api.Data.Enumerations;
using Cite.Api.Tests.Support;
using Cite.Api.ViewModels;

namespace Cite.Api.Tests.Controllers;

/// <summary>
/// <c>SubmissionCategoryController</c>: a submission's categories, read like the submission and changed with
/// EditEvaluation on its evaluation.
/// </summary>
public class SubmissionCategoryControllerTests(DatabaseFixture fixture, CiteAppFactory factory) : ApiTestBase(fixture, factory)
{
    /// <summary>A request with no identity is answered with a 401; the anonymous client is the case under test.</summary>
    [Fact]
    public async Task Get_without_an_identity_is_unauthorized()
    {
        await AssertStatus(HttpStatusCode.Unauthorized, await Client().GetAsync($"api/submissionCategories/{Guid.NewGuid()}", Ct));
    }

    [Fact]
    public async Task GetForSubmission_lists_the_categories_to_a_member_holding_ViewTeam()
    {
        var (graph, submission) = await Seed();
        var actor = await Actor().OnTeam(graph.Team.Id, permissions: [TeamPermission.ViewTeam]).SeedAsync();

        var categories = await ReadAsync<List<SubmissionCategory>>(await Client(actor).GetAsync($"api/submission/{submission.Submission.Id}/submissionCategories", Ct));

        Assert.Equal(submission.Category.Id, Assert.Single(categories).Id);
    }

    [Fact]
    public async Task GetForSubmission_lists_the_categories_to_a_caller_holding_ObserveEvaluation_on_the_evaluation()
    {
        var (graph, submission) = await Seed();
        var actor = await Actor().OnEvaluation(graph.Evaluation.Id, permissions: [EvaluationPermission.ObserveEvaluation]).SeedAsync();

        await AssertStatus(HttpStatusCode.OK, await Client(actor).GetAsync($"api/submission/{submission.Submission.Id}/submissionCategories", Ct));
    }

    [Theory]
    [InlineData(SystemPermission.ViewEvaluations)]
    [InlineData(SystemPermission.ObserveEvaluations)]
    public async Task GetForSubmission_lists_the_categories_to_a_caller_holding_a_system_permission(SystemPermission permission)
    {
        var (_, submission) = await Seed();
        var actor = await Actor().WithSystemPermissions(permission).SeedAsync();

        var categories = await ReadAsync<List<SubmissionCategory>>(await Client(actor).GetAsync($"api/submission/{submission.Submission.Id}/submissionCategories", Ct));

        Assert.Equal(submission.Category.Id, Assert.Single(categories).Id);
    }

    [Fact]
    public async Task GetForSubmission_is_forbidden_for_a_caller_holding_only_ExecuteEvaluations()
    {
        var (_, submission) = await Seed();
        var actor = await Actor().WithSystemPermissions(SystemPermission.ExecuteEvaluations).SeedAsync();

        await AssertApiError(HttpStatusCode.Forbidden, await Client(actor).GetAsync($"api/submission/{submission.Submission.Id}/submissionCategories", Ct));
    }

    [Fact]
    public async Task GetForSubmission_is_forbidden_for_a_member_holding_only_EditTeamScore()
    {
        var (graph, submission) = await Seed();
        var actor = await Actor().OnTeam(graph.Team.Id, permissions: [TeamPermission.EditTeamScore]).SeedAsync();

        await AssertApiError(HttpStatusCode.Forbidden, await Client(actor).GetAsync($"api/submission/{submission.Submission.Id}/submissionCategories", Ct));
    }

    [Fact]
    public async Task GetForSubmission_is_forbidden_for_a_caller_holding_ObserveEvaluation_only_on_another_evaluation()
    {
        var (_, submission) = await Seed();
        var actor = await Actor().OnNewEvaluation(EvaluationPermission.ObserveEvaluation).SeedAsync();

        await AssertApiError(HttpStatusCode.Forbidden, await Client(actor).GetAsync($"api/submission/{submission.Submission.Id}/submissionCategories", Ct));
    }

    [Fact]
    public async Task GetForSubmission_is_forbidden_for_a_caller_holding_only_ViewEvaluation_on_the_evaluation()
    {
        var (graph, submission) = await Seed();
        var actor = await Actor().OnEvaluation(graph.Evaluation.Id, permissions: [EvaluationPermission.ViewEvaluation]).SeedAsync();

        await AssertApiError(HttpStatusCode.Forbidden, await Client(actor).GetAsync($"api/submission/{submission.Submission.Id}/submissionCategories", Ct));
    }

    [Fact]
    public async Task Get_returns_the_category_to_a_member_holding_ViewTeam()
    {
        var (graph, submission) = await Seed();
        var actor = await Actor().OnTeam(graph.Team.Id, permissions: [TeamPermission.ViewTeam]).SeedAsync();

        var read = await ReadAsync<SubmissionCategory>(await Client(actor).GetAsync($"api/submissionCategories/{submission.Category.Id}", Ct));

        Assert.Equal(graph.Category.Id, read.ScoringCategoryId);
    }

    [Fact]
    public async Task Get_is_forbidden_for_a_caller_holding_ViewTeam_only_on_a_team_in_another_evaluation()
    {
        var (_, submission) = await Seed();
        var other = await TestScenario.SeedEvaluationAsync(Db, Ct, "Other");
        var actor = await Actor().OnNewTeam(other.Evaluation.Id, TeamPermission.ViewTeam).SeedAsync();

        await AssertStatus(HttpStatusCode.Forbidden, await Client(actor).GetAsync($"api/submissionCategories/{submission.Category.Id}", Ct));
    }

    [Fact]
    public async Task Get_returns_the_category_to_a_caller_holding_ObserveEvaluation_on_the_evaluation()
    {
        var (graph, submission) = await Seed();
        var actor = await Actor().OnEvaluation(graph.Evaluation.Id, permissions: [EvaluationPermission.ObserveEvaluation]).SeedAsync();

        var read = await ReadAsync<SubmissionCategory>(await Client(actor).GetAsync($"api/submissionCategories/{submission.Category.Id}", Ct));

        Assert.Equal(graph.Category.Id, read.ScoringCategoryId);
    }

    [Theory]
    [InlineData(SystemPermission.ViewEvaluations)]
    [InlineData(SystemPermission.ObserveEvaluations)]
    public async Task Get_returns_the_category_to_a_caller_holding_a_system_permission(SystemPermission permission)
    {
        var (graph, submission) = await Seed();
        var actor = await Actor().WithSystemPermissions(permission).SeedAsync();

        var read = await ReadAsync<SubmissionCategory>(await Client(actor).GetAsync($"api/submissionCategories/{submission.Category.Id}", Ct));

        Assert.Equal(graph.Category.Id, read.ScoringCategoryId);
    }

    [Fact]
    public async Task Get_is_forbidden_for_a_caller_holding_only_ExecuteEvaluations()
    {
        var (_, submission) = await Seed();
        var actor = await Actor().WithSystemPermissions(SystemPermission.ExecuteEvaluations).SeedAsync();

        await AssertApiError(HttpStatusCode.Forbidden, await Client(actor).GetAsync($"api/submissionCategories/{submission.Category.Id}", Ct));
    }

    [Fact]
    public async Task Get_is_forbidden_for_a_caller_holding_ObserveEvaluation_only_on_another_evaluation()
    {
        var (_, submission) = await Seed();
        var actor = await Actor().OnNewEvaluation(EvaluationPermission.ObserveEvaluation).SeedAsync();

        await AssertApiError(HttpStatusCode.Forbidden, await Client(actor).GetAsync($"api/submissionCategories/{submission.Category.Id}", Ct));
    }

    [Fact]
    public async Task Get_is_forbidden_for_a_caller_holding_only_ViewEvaluation_on_the_evaluation()
    {
        var (graph, submission) = await Seed();
        var actor = await Actor().OnEvaluation(graph.Evaluation.Id, permissions: [EvaluationPermission.ViewEvaluation]).SeedAsync();

        await AssertApiError(HttpStatusCode.Forbidden, await Client(actor).GetAsync($"api/submissionCategories/{submission.Category.Id}", Ct));
    }

    [Fact]
    public async Task Create_persists_the_category_for_a_caller_holding_EditEvaluation_on_the_evaluation()
    {
        var (graph, submission) = await Seed();
        var extra = TestData.ScoringCategory(graph.ScoringModel.Id, "Extra", 2);
        await Seed(extra);
        var actor = await Actor().OnEvaluation(graph.Evaluation.Id, permissions: [EvaluationPermission.EditEvaluation]).SeedAsync();
        var id = Guid.NewGuid();

        var response = await Client(actor).PostAsJsonAsync("api/submissionCategories", new { id, submissionId = submission.Submission.Id, scoringCategoryId = extra.Id }, Ct);

        await AssertStatus(HttpStatusCode.Created, response);
        await using var context = NewContext();
        Assert.Equal(extra.Id, (await context.SubmissionCategories.SingleAsync(x => x.Id == id, Ct)).ScoringCategoryId);
    }

    [Fact]
    public async Task Create_answers_a_scoring_category_of_another_scoring_model_with_conflict()
    {
        var (graph, submission) = await Seed();
        var other = await TestScenario.SeedEvaluationAsync(Db, Ct, "Other");
        var actor = await Actor().OnEvaluation(graph.Evaluation.Id, permissions: [EvaluationPermission.EditEvaluation]).SeedAsync();
        var id = Guid.NewGuid();

        await AssertApiError(HttpStatusCode.Conflict, await Client(actor).PostAsJsonAsync("api/submissionCategories", new { id, submissionId = submission.Submission.Id, scoringCategoryId = other.Category.Id }, Ct));

        await using var context = NewContext();
        Assert.False(await context.SubmissionCategories.AnyAsync(x => x.Id == id, Ct));
    }

    [Fact]
    public async Task Create_is_forbidden_for_a_caller_holding_only_ViewEvaluation_on_the_evaluation()
    {
        var (graph, submission) = await Seed();
        var actor = await Actor().OnEvaluation(graph.Evaluation.Id, permissions: [EvaluationPermission.ViewEvaluation]).SeedAsync();
        var id = Guid.NewGuid();

        await AssertStatus(HttpStatusCode.Forbidden, await Client(actor).PostAsJsonAsync("api/submissionCategories",
            new { id, submissionId = submission.Submission.Id, scoringCategoryId = graph.Category.Id }, Ct));

        await using var context = NewContext();
        Assert.False(await context.SubmissionCategories.AnyAsync(x => x.Id == id, Ct));
    }

    /// <summary>The score follows the selected options, and the category cannot move: nothing in the body is applied.</summary>
    [Fact]
    public async Task Update_changes_nothing_for_a_caller_holding_EditEvaluation_on_the_evaluation()
    {
        var (graph, submission) = await Seed();
        var other = await TestScenario.SeedEvaluationAsync(Db, Ct, "Other");
        var theirs = await TestScenario.SeedSubmissionAsync(Db, Ct, other, other.Team.Id);
        var actor = await Actor().OnEvaluation(graph.Evaluation.Id, permissions: [EvaluationPermission.EditEvaluation]).SeedAsync();

        var response = await Client(actor).PutAsJsonAsync($"api/submissionCategories/{submission.Category.Id}",
            new { id = submission.Category.Id, submissionId = theirs.Submission.Id, scoringCategoryId = other.Category.Id, score = 7.0 }, Ct);

        await AssertStatus(HttpStatusCode.OK, response);
        await using var context = NewContext();
        var stored = await context.SubmissionCategories.SingleAsync(x => x.Id == submission.Category.Id, Ct);
        Assert.Equal((submission.Category.Score, submission.Submission.Id, graph.Category.Id), (stored.Score, stored.SubmissionId, stored.ScoringCategoryId));
    }

    [Fact]
    public async Task Update_is_forbidden_for_a_caller_holding_EditEvaluation_only_on_another_evaluation()
    {
        var (graph, submission) = await Seed();
        var actor = await Actor().OnNewEvaluation(EvaluationPermission.EditEvaluation).SeedAsync();

        await AssertStatus(HttpStatusCode.Forbidden, await Client(actor).PutAsJsonAsync($"api/submissionCategories/{submission.Category.Id}",
            new { id = submission.Category.Id, submissionId = submission.Submission.Id, scoringCategoryId = graph.Category.Id, score = 7.0 }, Ct));

        await using var context = NewContext();
        Assert.Equal(0.0, (await context.SubmissionCategories.SingleAsync(x => x.Id == submission.Category.Id, Ct)).Score);
    }

    [Fact]
    public async Task Delete_removes_the_category_for_a_caller_holding_EditEvaluation_on_the_evaluation()
    {
        var (graph, submission) = await Seed();
        var actor = await Actor().OnEvaluation(graph.Evaluation.Id, permissions: [EvaluationPermission.EditEvaluation]).SeedAsync();

        await AssertStatus(HttpStatusCode.NoContent, await Client(actor).DeleteAsync($"api/submissionCategories/{submission.Category.Id}", Ct));

        await using var context = NewContext();
        Assert.False(await context.SubmissionCategories.AnyAsync(x => x.Id == submission.Category.Id, Ct));
    }

    [Fact]
    public async Task Delete_is_forbidden_for_a_member_holding_SubmitTeamScore()
    {
        var (graph, submission) = await Seed();
        var actor = await Actor().OnTeam(graph.Team.Id, permissions: [TeamPermission.SubmitTeamScore]).SeedAsync();

        await AssertStatus(HttpStatusCode.Forbidden, await Client(actor).DeleteAsync($"api/submissionCategories/{submission.Category.Id}", Ct));

        await using var context = NewContext();
        Assert.True(await context.SubmissionCategories.AnyAsync(x => x.Id == submission.Category.Id, Ct));
    }

    [Fact]
    public async Task Create_persists_the_category_for_a_caller_holding_EditEvaluations()
    {
        var (graph, submission) = await Seed();
        var extra = TestData.ScoringCategory(graph.ScoringModel.Id, "Extra", 2);
        await Seed(extra);
        var actor = await Actor().WithSystemPermissions(SystemPermission.EditEvaluations).SeedAsync();
        var id = Guid.NewGuid();

        await AssertStatus(HttpStatusCode.Created, await Client(actor).PostAsJsonAsync("api/submissionCategories", new { id, submissionId = submission.Submission.Id, scoringCategoryId = extra.Id }, Ct));

        await using var context = NewContext();
        Assert.Equal(extra.Id, (await context.SubmissionCategories.SingleAsync(x => x.Id == id, Ct)).ScoringCategoryId);
    }

    [Fact]
    public async Task Create_is_forbidden_for_a_caller_holding_only_ViewEvaluations()
    {
        var (graph, submission) = await Seed();
        var actor = await Actor().WithSystemPermissions(SystemPermission.ViewEvaluations).SeedAsync();
        var id = Guid.NewGuid();

        await AssertApiError(HttpStatusCode.Forbidden, await Client(actor).PostAsJsonAsync("api/submissionCategories",
            new { id, submissionId = submission.Submission.Id, scoringCategoryId = graph.Category.Id }, Ct));

        await using var context = NewContext();
        Assert.False(await context.SubmissionCategories.AnyAsync(x => x.Id == id, Ct));
    }

    [Fact]
    public async Task Update_answers_the_category_unchanged_for_a_caller_holding_EditEvaluations()
    {
        var (_, submission) = await Seed();
        var actor = await Actor().WithSystemPermissions(SystemPermission.EditEvaluations).SeedAsync();

        var read = await ReadAsync<SubmissionCategory>(await Client(actor).PutAsJsonAsync($"api/submissionCategories/{submission.Category.Id}", new { score = 7.0 }, Ct));

        Assert.Equal(submission.Category.Score, read.Score);
    }

    [Fact]
    public async Task Update_is_forbidden_for_a_caller_holding_only_ViewEvaluations()
    {
        var (graph, submission) = await Seed();
        var actor = await Actor().WithSystemPermissions(SystemPermission.ViewEvaluations).SeedAsync();

        await AssertApiError(HttpStatusCode.Forbidden, await Client(actor).PutAsJsonAsync($"api/submissionCategories/{submission.Category.Id}",
            new { id = submission.Category.Id, submissionId = submission.Submission.Id, scoringCategoryId = graph.Category.Id, score = 7.0 }, Ct));

        await using var context = NewContext();
        Assert.Equal(0.0, (await context.SubmissionCategories.SingleAsync(x => x.Id == submission.Category.Id, Ct)).Score);
    }

    [Fact]
    public async Task Delete_removes_the_category_for_a_caller_holding_EditEvaluations()
    {
        var (_, submission) = await Seed();
        var actor = await Actor().WithSystemPermissions(SystemPermission.EditEvaluations).SeedAsync();

        await AssertStatus(HttpStatusCode.NoContent, await Client(actor).DeleteAsync($"api/submissionCategories/{submission.Category.Id}", Ct));

        await using var context = NewContext();
        Assert.False(await context.SubmissionCategories.AnyAsync(x => x.Id == submission.Category.Id, Ct));
    }

    [Fact]
    public async Task Delete_is_forbidden_for_a_caller_holding_only_ViewEvaluations()
    {
        var (_, submission) = await Seed();
        var actor = await Actor().WithSystemPermissions(SystemPermission.ViewEvaluations).SeedAsync();

        await AssertApiError(HttpStatusCode.Forbidden, await Client(actor).DeleteAsync($"api/submissionCategories/{submission.Category.Id}", Ct));

        await using var context = NewContext();
        Assert.True(await context.SubmissionCategories.AnyAsync(x => x.Id == submission.Category.Id, Ct));
    }

    [Fact]
    public async Task Delete_is_forbidden_for_a_caller_holding_EditEvaluation_only_on_another_evaluation()
    {
        var (_, submission) = await Seed();
        var actor = await Actor().OnNewEvaluation(EvaluationPermission.EditEvaluation).SeedAsync();

        await AssertApiError(HttpStatusCode.Forbidden, await Client(actor).DeleteAsync($"api/submissionCategories/{submission.Category.Id}", Ct));

        await using var context = NewContext();
        Assert.True(await context.SubmissionCategories.AnyAsync(x => x.Id == submission.Category.Id, Ct));
    }

    private async Task<(EvaluationGraph Graph, SubmissionGraph Submission)> Seed()
    {
        var graph = await TestScenario.SeedEvaluationAsync(Db, Ct);

        return (graph, await TestScenario.SeedSubmissionAsync(Db, Ct, graph, graph.Team.Id));
    }
}
