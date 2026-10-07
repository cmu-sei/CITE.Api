// Copyright 2026 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.

using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Cite.Api.Data.Enumerations;
using Cite.Api.Hubs;
using Cite.Api.Tests.Support;
using Cite.Api.ViewModels;

namespace Cite.Api.Tests.Controllers;

/// <summary>
/// <c>SubmissionOptionController</c>: a submission's options, read like the submission, selected with the
/// team's EditTeamScore, and managed with ManageEvaluation on the evaluation.
/// </summary>
public class SubmissionOptionControllerTests(DatabaseFixture fixture, CiteAppFactory factory) : ApiTestBase(fixture, factory)
{
    /// <summary>A request with no identity is answered with a 401; the anonymous client is the case under test.</summary>
    [Fact]
    public async Task Get_without_an_identity_is_unauthorized()
    {
        await AssertStatus(HttpStatusCode.Unauthorized, await Client().GetAsync($"api/submissionOptions/{Guid.NewGuid()}", Ct));
    }

    [Fact]
    public async Task GetForSubmissionCategory_lists_the_options_to_a_member_holding_ViewTeam()
    {
        var (graph, submission) = await Seed();
        var actor = await Actor().OnTeam(graph.Team.Id, permissions: [TeamPermission.ViewTeam]).SeedAsync();

        var options = await ReadAsync<List<SubmissionOption>>(await Client(actor).GetAsync($"api/submissionCategory/{submission.Category.Id}/submissionOptions", Ct));

        Assert.Equal(submission.Option.Id, Assert.Single(options).Id);
    }

    [Fact]
    public async Task GetForSubmissionCategory_is_forbidden_for_a_member_holding_only_EditTeamScore()
    {
        var (graph, submission) = await Seed();
        var actor = await Actor().OnTeam(graph.Team.Id, permissions: [TeamPermission.EditTeamScore]).SeedAsync();

        await AssertApiError(HttpStatusCode.Forbidden, await Client(actor).GetAsync($"api/submissionCategory/{submission.Category.Id}/submissionOptions", Ct));
    }

    [Fact]
    public async Task Get_returns_the_option_to_a_caller_holding_ObserveEvaluation_on_the_evaluation()
    {
        var (graph, submission) = await Seed();
        var actor = await Actor().OnEvaluation(graph.Evaluation.Id, permissions: [EvaluationPermission.ObserveEvaluation]).SeedAsync();

        var read = await ReadAsync<SubmissionOption>(await Client(actor).GetAsync($"api/submissionOptions/{submission.Option.Id}", Ct));

        Assert.Equal(graph.Option.Id, read.ScoringOptionId);
    }

    [Fact]
    public async Task Get_is_forbidden_for_a_caller_holding_ObserveEvaluation_only_on_another_evaluation()
    {
        var (_, submission) = await Seed();
        var actor = await Actor().OnNewEvaluation(EvaluationPermission.ObserveEvaluation).SeedAsync();

        await AssertStatus(HttpStatusCode.Forbidden, await Client(actor).GetAsync($"api/submissionOptions/{submission.Option.Id}", Ct));
    }

    [Fact]
    public async Task SetOptionTrue_selects_the_option_and_scores_the_submission_for_a_member_holding_EditTeamScore()
    {
        var (graph, submission) = await Seed();
        var actor = await Actor().OnTeam(graph.Team.Id, permissions: [TeamPermission.EditTeamScore]).SeedAsync();

        var scored = await ReadAsync<Submission>(await Client(actor).PutAsync($"api/submissionOptions/{submission.Option.Id}/select", null, Ct));

        await using var context = NewContext();
        Assert.True((await context.SubmissionOptions.SingleAsync(x => x.Id == submission.Option.Id, Ct)).IsSelected);
        Assert.Equal(submission.Submission.Id, scored.Id);
    }

    [Fact]
    public async Task SetOptionTrue_broadcasts_the_updated_submission_to_the_team()
    {
        var (graph, submission) = await Seed();
        var actor = await Actor().OnTeam(graph.Team.Id, permissions: [TeamPermission.EditTeamScore]).SeedAsync();

        await AssertStatus(HttpStatusCode.OK, await Client(actor).PutAsync($"api/submissionOptions/{submission.Option.Id}/select", null, Ct));

        Assert.Contains(Factory.Hub<MainHub>().ToGroup(graph.Team.Id),
            x => x.Method == MainHubMethods.SubmissionUpdated && ((Submission)x.Arguments[0]).Id == submission.Submission.Id);
    }

    [Fact]
    public async Task SetOptionTrue_is_forbidden_for_a_member_holding_only_ViewTeam()
    {
        var (graph, submission) = await Seed();
        var actor = await Actor().OnTeam(graph.Team.Id, permissions: [TeamPermission.ViewTeam]).SeedAsync();

        await AssertStatus(HttpStatusCode.Forbidden, await Client(actor).PutAsync($"api/submissionOptions/{submission.Option.Id}/select", null, Ct));

        await using var context = NewContext();
        Assert.False((await context.SubmissionOptions.SingleAsync(x => x.Id == submission.Option.Id, Ct)).IsSelected);
    }

    // Same case as SubmissionControllerTests.Get_returns_another_teams_submission_to_a_member_of_a_sibling_team_holding_ViewTeam.
    [Fact]
    public async Task SetOptionTrue_selects_an_option_of_another_teams_submission_for_a_member_of_a_sibling_team_holding_EditTeamScore()
    {
        var (graph, submission) = await Seed();
        var actor = await Actor().OnNewTeam(graph.Evaluation.Id, TeamPermission.EditTeamScore).SeedAsync();

        await AssertStatus(HttpStatusCode.OK, await Client(actor).PutAsync($"api/submissionOptions/{submission.Option.Id}/select", null, Ct));

        await using var context = NewContext();
        Assert.True((await context.SubmissionOptions.SingleAsync(x => x.Id == submission.Option.Id, Ct)).IsSelected);
    }

    [Fact]
    public async Task SetOptionFalse_deselects_the_option_for_a_member_holding_EditTeamScore()
    {
        var (graph, submission) = await Seed();
        submission.Option.IsSelected = true;
        await Db.SaveChangesAsync(Ct);
        var actor = await Actor().OnTeam(graph.Team.Id, permissions: [TeamPermission.EditTeamScore]).SeedAsync();

        await AssertStatus(HttpStatusCode.OK, await Client(actor).PutAsync($"api/submissionOptions/{submission.Option.Id}/deselect", null, Ct));

        await using var context = NewContext();
        Assert.False((await context.SubmissionOptions.SingleAsync(x => x.Id == submission.Option.Id, Ct)).IsSelected);
    }

    [Fact]
    public async Task SetOptionFalse_is_forbidden_for_a_member_holding_EditTeamScore_only_in_another_evaluation()
    {
        var (_, submission) = await Seed();
        submission.Option.IsSelected = true;
        await Db.SaveChangesAsync(Ct);
        var other = await TestScenario.SeedEvaluationAsync(Db, Ct, "Other");
        var actor = await Actor().OnNewTeam(other.Evaluation.Id, TeamPermission.EditTeamScore).SeedAsync();

        await AssertStatus(HttpStatusCode.Forbidden, await Client(actor).PutAsync($"api/submissionOptions/{submission.Option.Id}/deselect", null, Ct));

        await using var context = NewContext();
        Assert.True((await context.SubmissionOptions.SingleAsync(x => x.Id == submission.Option.Id, Ct)).IsSelected);
    }

    [Fact]
    public async Task Create_persists_the_option_for_a_caller_holding_ManageEvaluation_on_the_evaluation()
    {
        var (graph, submission) = await Seed();
        var actor = await Actor().OnEvaluation(graph.Evaluation.Id, permissions: [EvaluationPermission.ManageEvaluation]).SeedAsync();
        var id = Guid.NewGuid();

        var response = await Client(actor).PostAsJsonAsync("api/submissionOptions", new { id, submissionCategoryId = submission.Category.Id, scoringOptionId = graph.Option.Id }, Ct);

        await AssertStatus(HttpStatusCode.Created, response);
        await using var context = NewContext();
        Assert.True(await context.SubmissionOptions.AnyAsync(x => x.Id == id, Ct));
    }

    [Fact]
    public async Task Create_is_forbidden_for_a_caller_holding_only_EditEvaluation_on_the_evaluation()
    {
        var (graph, submission) = await Seed();
        var actor = await Actor().OnEvaluation(graph.Evaluation.Id, permissions: [EvaluationPermission.EditEvaluation]).SeedAsync();
        var id = Guid.NewGuid();

        await AssertStatus(HttpStatusCode.Forbidden, await Client(actor).PostAsJsonAsync("api/submissionOptions",
            new { id, submissionCategoryId = submission.Category.Id, scoringOptionId = graph.Option.Id }, Ct));

        await using var context = NewContext();
        Assert.False(await context.SubmissionOptions.AnyAsync(x => x.Id == id, Ct));
    }

    [Fact]
    public async Task Update_changes_the_option_for_a_caller_holding_ManageEvaluations()
    {
        var (graph, submission) = await Seed();
        var actor = await Actor().WithSystemPermissions(SystemPermission.ManageEvaluations).SeedAsync();

        var response = await Client(actor).PutAsJsonAsync($"api/submissionOptions/{submission.Option.Id}",
            new { id = submission.Option.Id, submissionCategoryId = submission.Category.Id, scoringOptionId = graph.Option.Id, isSelected = true }, Ct);

        await AssertStatus(HttpStatusCode.OK, response);
        await using var context = NewContext();
        Assert.True((await context.SubmissionOptions.SingleAsync(x => x.Id == submission.Option.Id, Ct)).IsSelected);
    }

    [Fact]
    public async Task Update_is_forbidden_for_a_caller_holding_ManageEvaluation_only_on_another_evaluation()
    {
        var (graph, submission) = await Seed();
        var actor = await Actor().OnNewEvaluation(EvaluationPermission.ManageEvaluation).SeedAsync();

        await AssertStatus(HttpStatusCode.Forbidden, await Client(actor).PutAsJsonAsync($"api/submissionOptions/{submission.Option.Id}",
            new { id = submission.Option.Id, submissionCategoryId = submission.Category.Id, scoringOptionId = graph.Option.Id, isSelected = true }, Ct));

        await using var context = NewContext();
        Assert.False((await context.SubmissionOptions.SingleAsync(x => x.Id == submission.Option.Id, Ct)).IsSelected);
    }

    /// <summary>ManageEvaluation on the evaluation of the category named in the body moves another evaluation's option into it.</summary>
    [Fact]
    public async Task Update_moves_another_evaluations_option_into_a_category_of_the_evaluation_the_caller_manages()
    {
        var (managed, mine) = await Seed();
        var (other, theirs) = await Seed();
        var actor = await Actor().OnEvaluation(managed.Evaluation.Id, permissions: [EvaluationPermission.ManageEvaluation]).SeedAsync();

        var response = await Client(actor).PutAsJsonAsync($"api/submissionOptions/{theirs.Option.Id}",
            new { id = theirs.Option.Id, submissionCategoryId = mine.Category.Id, scoringOptionId = other.Option.Id }, Ct);

        await AssertStatus(HttpStatusCode.OK, response);
        await using var context = NewContext();
        Assert.Equal(mine.Category.Id, (await context.SubmissionOptions.SingleAsync(x => x.Id == theirs.Option.Id, Ct)).SubmissionCategoryId);
    }

    [Fact]
    public async Task Delete_removes_the_option_for_a_caller_holding_ManageEvaluation_on_the_evaluation()
    {
        var (graph, submission) = await Seed();
        var actor = await Actor().OnEvaluation(graph.Evaluation.Id, permissions: [EvaluationPermission.ManageEvaluation]).SeedAsync();

        await AssertStatus(HttpStatusCode.NoContent, await Client(actor).DeleteAsync($"api/submissionOptions/{submission.Option.Id}", Ct));

        await using var context = NewContext();
        Assert.False(await context.SubmissionOptions.AnyAsync(x => x.Id == submission.Option.Id, Ct));
    }

    [Fact]
    public async Task Delete_is_forbidden_for_a_caller_holding_only_EditEvaluation_on_the_evaluation()
    {
        var (graph, submission) = await Seed();
        var actor = await Actor().OnEvaluation(graph.Evaluation.Id, permissions: [EvaluationPermission.EditEvaluation]).SeedAsync();

        await AssertStatus(HttpStatusCode.Forbidden, await Client(actor).DeleteAsync($"api/submissionOptions/{submission.Option.Id}", Ct));

        await using var context = NewContext();
        Assert.True(await context.SubmissionOptions.AnyAsync(x => x.Id == submission.Option.Id, Ct));
    }

    private async Task<(EvaluationGraph Graph, SubmissionGraph Submission)> Seed()
    {
        var graph = await TestScenario.SeedEvaluationAsync(Db, Ct);

        return (graph, await TestScenario.SeedSubmissionAsync(Db, Ct, graph, graph.Team.Id));
    }
}
