// Copyright 2026 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.

using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Cite.Api.Data.Enumerations;
using Cite.Api.Data.Models;
using Cite.Api.Tests.Support;
using Cite.Api.ViewModels;

namespace Cite.Api.Tests.Controllers;

/// <summary>
/// <c>SubmissionCommentController</c>: comments on a submission option, read like the submission and, here,
/// also written with the view permission <c>SubmissionService.HasSpecificPermission</c> checks.
/// </summary>
public class SubmissionCommentControllerTests(DatabaseFixture fixture, CiteAppFactory factory) : ApiTestBase(fixture, factory)
{
    /// <summary>A request with no identity is answered with a 401; the anonymous client is the case under test.</summary>
    [Fact]
    public async Task Get_without_an_identity_is_unauthorized()
    {
        await AssertStatus(HttpStatusCode.Unauthorized, await Client().GetAsync($"api/submissionComments/{Guid.NewGuid()}", Ct));
    }

    [Fact]
    public async Task GetForSubmissionOption_lists_the_comments_to_a_member_holding_ViewTeam()
    {
        var (graph, submission, comment) = await Seed();
        var actor = await Actor().OnTeam(graph.Team.Id, permissions: [TeamPermission.ViewTeam]).SeedAsync();

        var comments = await ReadAsync<List<SubmissionComment>>(await Client(actor).GetAsync($"api/submissionOption/{submission.Option.Id}/submissionComments", Ct));

        Assert.Equal(comment.Id, Assert.Single(comments).Id);
    }

    [Fact]
    public async Task GetForSubmissionOption_is_forbidden_for_a_member_holding_only_SubmitTeamScore()
    {
        var (graph, submission, _) = await Seed();
        var actor = await Actor().OnTeam(graph.Team.Id, permissions: [TeamPermission.SubmitTeamScore]).SeedAsync();

        await AssertApiError(HttpStatusCode.Forbidden, await Client(actor).GetAsync($"api/submissionOption/{submission.Option.Id}/submissionComments", Ct));
    }

    [Fact]
    public async Task GetForSubmissionOption_lists_the_comments_to_a_caller_holding_ObserveEvaluation_on_the_evaluation()
    {
        var (graph, submission, comment) = await Seed();
        var actor = await Actor().OnEvaluation(graph.Evaluation.Id, permissions: [EvaluationPermission.ObserveEvaluation]).SeedAsync();

        var comments = await ReadAsync<List<SubmissionComment>>(await Client(actor).GetAsync($"api/submissionOption/{submission.Option.Id}/submissionComments", Ct));

        Assert.Equal(comment.Id, Assert.Single(comments).Id);
    }

    [Fact]
    public async Task GetForSubmissionOption_lists_the_comments_to_a_caller_holding_ObserveEvaluations()
    {
        var (_, submission, comment) = await Seed();
        var actor = await Actor().WithSystemPermissions(SystemPermission.ObserveEvaluations).SeedAsync();

        var comments = await ReadAsync<List<SubmissionComment>>(await Client(actor).GetAsync($"api/submissionOption/{submission.Option.Id}/submissionComments", Ct));

        Assert.Equal(comment.Id, Assert.Single(comments).Id);
    }

    [Fact]
    public async Task GetForSubmissionOption_is_forbidden_for_a_caller_holding_ObserveEvaluation_only_on_another_evaluation()
    {
        var (_, submission, _) = await Seed();
        var actor = await Actor().OnNewEvaluation(EvaluationPermission.ObserveEvaluation).SeedAsync();

        await AssertApiError(HttpStatusCode.Forbidden, await Client(actor).GetAsync($"api/submissionOption/{submission.Option.Id}/submissionComments", Ct));
    }

    [Fact]
    public async Task GetForSubmissionOption_is_forbidden_for_a_caller_holding_only_ViewEvaluation_on_the_evaluation()
    {
        var (graph, submission, _) = await Seed();
        var actor = await Actor().OnEvaluation(graph.Evaluation.Id, permissions: [EvaluationPermission.ViewEvaluation]).SeedAsync();

        await AssertApiError(HttpStatusCode.Forbidden, await Client(actor).GetAsync($"api/submissionOption/{submission.Option.Id}/submissionComments", Ct));
    }

    [Fact]
    public async Task Get_returns_the_comment_to_a_caller_holding_ObserveEvaluation_on_the_evaluation()
    {
        var (graph, _, comment) = await Seed();
        var actor = await Actor().OnEvaluation(graph.Evaluation.Id, permissions: [EvaluationPermission.ObserveEvaluation]).SeedAsync();

        var read = await ReadAsync<SubmissionComment>(await Client(actor).GetAsync($"api/submissionComments/{comment.Id}", Ct));

        Assert.Equal(comment.Comment, read.Comment);
    }

    [Fact]
    public async Task Get_is_forbidden_for_a_caller_holding_ObserveEvaluation_only_on_another_evaluation()
    {
        var (_, _, comment) = await Seed();
        var actor = await Actor().OnNewEvaluation(EvaluationPermission.ObserveEvaluation).SeedAsync();

        await AssertStatus(HttpStatusCode.Forbidden, await Client(actor).GetAsync($"api/submissionComments/{comment.Id}", Ct));
    }

    [Fact]
    public async Task Get_returns_the_comment_to_a_member_holding_ViewTeam()
    {
        var (graph, _, comment) = await Seed();
        var actor = await Actor().OnTeam(graph.Team.Id, permissions: [TeamPermission.ViewTeam]).SeedAsync();

        var read = await ReadAsync<SubmissionComment>(await Client(actor).GetAsync($"api/submissionComments/{comment.Id}", Ct));

        Assert.Equal(comment.Comment, read.Comment);
    }

    [Fact]
    public async Task Get_is_forbidden_for_a_caller_holding_only_ViewEvaluation_on_the_evaluation()
    {
        var (graph, _, comment) = await Seed();
        var actor = await Actor().OnEvaluation(graph.Evaluation.Id, permissions: [EvaluationPermission.ViewEvaluation]).SeedAsync();

        await AssertApiError(HttpStatusCode.Forbidden, await Client(actor).GetAsync($"api/submissionComments/{comment.Id}", Ct));
    }

    /// <summary>A member holding only ViewTeam adds a comment.</summary>
    [Fact]
    public async Task Create_stores_a_comment_for_a_member_holding_only_ViewTeam()
    {
        var (graph, submission, _) = await Seed();
        var actor = await Actor().OnTeam(graph.Team.Id, permissions: [TeamPermission.ViewTeam]).SeedAsync();
        var id = Guid.NewGuid();

        var response = await Client(actor).PostAsJsonAsync("api/submissionComments", new { id, submissionOptionId = submission.Option.Id, comment = "Viewer's note" }, Ct);

        await AssertStatus(HttpStatusCode.Created, response);
        await using var context = NewContext();
        Assert.Equal("Viewer's note", (await context.SubmissionComments.SingleAsync(x => x.Id == id, Ct)).Comment);
    }

    [Fact]
    public async Task Create_is_forbidden_for_a_member_holding_only_EditTeamScore()
    {
        var (graph, submission, _) = await Seed();
        var actor = await Actor().OnTeam(graph.Team.Id, permissions: [TeamPermission.EditTeamScore]).SeedAsync();
        var id = Guid.NewGuid();

        await AssertStatus(HttpStatusCode.Forbidden, await Client(actor).PostAsJsonAsync("api/submissionComments",
            new { id, submissionOptionId = submission.Option.Id, comment = "Refused" }, Ct));

        await using var context = NewContext();
        Assert.False(await context.SubmissionComments.AnyAsync(x => x.Id == id, Ct));
    }

    // Same case as Create_stores_a_comment_for_a_member_holding_only_ViewTeam.
    [Fact]
    public async Task Update_changes_the_comment_for_a_member_holding_only_ViewTeam()
    {
        var (graph, submission, comment) = await Seed();
        var actor = await Actor().OnTeam(graph.Team.Id, permissions: [TeamPermission.ViewTeam]).SeedAsync();

        var response = await Client(actor).PutAsJsonAsync($"api/submissionComments/{comment.Id}",
            new { id = comment.Id, submissionOptionId = submission.Option.Id, comment = "Changed" }, Ct);

        await AssertStatus(HttpStatusCode.OK, response);
        await using var context = NewContext();
        Assert.Equal("Changed", (await context.SubmissionComments.SingleAsync(x => x.Id == comment.Id, Ct)).Comment);
    }

    [Fact]
    public async Task Update_is_forbidden_for_a_caller_holding_ViewTeam_only_on_a_team_in_another_evaluation()
    {
        var (_, submission, comment) = await Seed();
        var other = await TestScenario.SeedEvaluationAsync(Db, Ct, "Other");
        var actor = await Actor().OnNewTeam(other.Evaluation.Id, TeamPermission.ViewTeam).SeedAsync();

        await AssertStatus(HttpStatusCode.Forbidden, await Client(actor).PutAsJsonAsync($"api/submissionComments/{comment.Id}",
            new { id = comment.Id, submissionOptionId = submission.Option.Id, comment = "Changed" }, Ct));

        await using var context = NewContext();
        Assert.Equal(comment.Comment, (await context.SubmissionComments.SingleAsync(x => x.Id == comment.Id, Ct)).Comment);
    }

    // Same case as Create_stores_a_comment_for_a_member_holding_only_ViewTeam.
    [Fact]
    public async Task Delete_removes_the_comment_for_a_member_holding_ViewTeam()
    {
        var (graph, _, comment) = await Seed();
        var actor = await Actor().OnTeam(graph.Team.Id, permissions: [TeamPermission.ViewTeam]).SeedAsync();

        await AssertStatus(HttpStatusCode.NoContent, await Client(actor).DeleteAsync($"api/submissionComments/{comment.Id}", Ct));

        await using var context = NewContext();
        Assert.False(await context.SubmissionComments.AnyAsync(x => x.Id == comment.Id, Ct));
    }

    [Fact]
    public async Task Delete_is_forbidden_for_a_member_holding_only_EditTeamScore()
    {
        var (graph, _, comment) = await Seed();
        var actor = await Actor().OnTeam(graph.Team.Id, permissions: [TeamPermission.EditTeamScore]).SeedAsync();

        await AssertStatus(HttpStatusCode.Forbidden, await Client(actor).DeleteAsync($"api/submissionComments/{comment.Id}", Ct));

        await using var context = NewContext();
        Assert.True(await context.SubmissionComments.AnyAsync(x => x.Id == comment.Id, Ct));
    }

    private async Task<(EvaluationGraph Graph, SubmissionGraph Submission, SubmissionCommentEntity Comment)> Seed()
    {
        var graph = await TestScenario.SeedEvaluationAsync(Db, Ct);
        var submission = await TestScenario.SeedSubmissionAsync(Db, Ct, graph, graph.Team.Id);
        var comment = TestData.SubmissionComment(submission.Option.Id);
        await Seed(comment);

        return (graph, submission, comment);
    }
}
