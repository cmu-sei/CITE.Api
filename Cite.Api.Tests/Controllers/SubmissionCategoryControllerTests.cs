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

    [Fact]
    public async Task Get_returns_the_category_to_a_caller_holding_ViewEvaluations()
    {
        var (graph, submission) = await Seed();
        var actor = await Actor().WithSystemPermissions(SystemPermission.ViewEvaluations).SeedAsync();

        var read = await ReadAsync<SubmissionCategory>(await Client(actor).GetAsync($"api/submissionCategories/{submission.Category.Id}", Ct));

        Assert.Equal(graph.Category.Id, read.ScoringCategoryId);
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

    [Fact]
    public async Task Update_changes_the_score_for_a_caller_holding_EditEvaluation_on_the_evaluation()
    {
        var (graph, submission) = await Seed();
        var actor = await Actor().OnEvaluation(graph.Evaluation.Id, permissions: [EvaluationPermission.EditEvaluation]).SeedAsync();

        var response = await Client(actor).PutAsJsonAsync($"api/submissionCategories/{submission.Category.Id}",
            new { id = submission.Category.Id, submissionId = submission.Submission.Id, scoringCategoryId = graph.Category.Id, score = 7.0 }, Ct);

        await AssertStatus(HttpStatusCode.OK, response);
        await using var context = NewContext();
        Assert.Equal(7.0, (await context.SubmissionCategories.SingleAsync(x => x.Id == submission.Category.Id, Ct)).Score);
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

    private async Task<(EvaluationGraph Graph, SubmissionGraph Submission)> Seed()
    {
        var graph = await TestScenario.SeedEvaluationAsync(Db, Ct);

        return (graph, await TestScenario.SeedSubmissionAsync(Db, Ct, graph, graph.Team.Id));
    }
}
