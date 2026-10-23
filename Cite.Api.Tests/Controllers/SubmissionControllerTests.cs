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
/// <c>SubmissionController</c>: scoresheets. Managing them takes ManageEvaluation; reading and scoring one
/// takes an evaluation-wide permission or the team permission <c>SubmissionService.HasSpecificPermission</c>
/// derives from the caller's own team in the evaluation.
/// </summary>
public class SubmissionControllerTests(DatabaseFixture fixture, CiteAppFactory factory) : ApiTestBase(fixture, factory)
{
    /// <summary>A request with no identity is answered with a 401; the anonymous client is the case under test.</summary>
    [Fact]
    public async Task GetAll_without_an_identity_is_unauthorized()
    {
        await AssertStatus(HttpStatusCode.Unauthorized, await Client().GetAsync("api/submissions", Ct));
    }

    [Fact]
    public async Task GetAll_lists_the_submissions_of_an_evaluation_for_a_caller_holding_ManageEvaluations()
    {
        var graph = await Seed();
        var submission = await SeedSubmission(graph, graph.Team.Id);
        var actor = await Actor().WithSystemPermissions(SystemPermission.ManageEvaluations).SeedAsync();

        var submissions = await ReadAsync<List<Submission>>(await Client(actor).GetAsync($"api/submissions?evaluationId={graph.Evaluation.Id}", Ct));

        Assert.Equal(submission.Submission.Id, Assert.Single(submissions).Id);
    }

    [Fact]
    public async Task GetAll_is_forbidden_for_a_caller_holding_only_ViewEvaluations()
    {
        var actor = await Actor().WithSystemPermissions(SystemPermission.ViewEvaluations).SeedAsync();

        await AssertApiError(HttpStatusCode.Forbidden, await Client(actor).GetAsync("api/submissions", Ct));
    }

    [Fact]
    public async Task GetByEvaluation_lists_the_submissions_up_to_the_current_move_for_a_caller_holding_ManageEvaluation_on_it()
    {
        var graph = await Seed();
        var current = await SeedSubmission(graph, graph.Team.Id);
        await SeedSubmission(graph, graph.Team.Id, moveNumber: 1);
        var actor = await Actor().OnEvaluation(graph.Evaluation.Id, permissions: [EvaluationPermission.ManageEvaluation]).SeedAsync();

        var submissions = await ReadAsync<List<Submission>>(await Client(actor).GetAsync($"api/evaluations/{graph.Evaluation.Id}/submissions", Ct));

        Assert.Equal(current.Submission.Id, Assert.Single(submissions).Id);
    }

    [Fact]
    public async Task GetByEvaluation_is_forbidden_for_a_caller_holding_only_EditEvaluation()
    {
        var graph = await Seed();
        var actor = await Actor().OnEvaluation(graph.Evaluation.Id, permissions: [EvaluationPermission.EditEvaluation]).SeedAsync();

        await AssertStatus(HttpStatusCode.Forbidden, await Client(actor).GetAsync($"api/evaluations/{graph.Evaluation.Id}/submissions", Ct));
    }

    [Fact]
    public async Task GetByEvaluation_is_forbidden_for_a_caller_holding_ManageEvaluation_only_on_another_evaluation()
    {
        var graph = await Seed();
        var actor = await Actor().OnNewEvaluation(EvaluationPermission.ManageEvaluation).SeedAsync();

        await AssertStatus(HttpStatusCode.Forbidden, await Client(actor).GetAsync($"api/evaluations/{graph.Evaluation.Id}/submissions", Ct));
    }

    [Fact]
    public async Task GetByEvaluation_lists_the_submissions_up_to_the_current_move_for_a_caller_holding_ManageEvaluations()
    {
        var graph = await Seed();
        var current = await SeedSubmission(graph, graph.Team.Id);
        await SeedSubmission(graph, graph.Team.Id, moveNumber: 1);
        var actor = await Actor().WithSystemPermissions(SystemPermission.ManageEvaluations).SeedAsync();

        var submissions = await ReadAsync<List<Submission>>(await Client(actor).GetAsync($"api/evaluations/{graph.Evaluation.Id}/submissions", Ct));

        Assert.Equal(current.Submission.Id, Assert.Single(submissions).Id);
    }

    [Fact]
    public async Task GetByEvaluation_is_forbidden_for_a_caller_holding_only_EditEvaluations()
    {
        var graph = await Seed();
        var actor = await Actor().WithSystemPermissions(SystemPermission.EditEvaluations).SeedAsync();

        await AssertApiError(HttpStatusCode.Forbidden, await Client(actor).GetAsync($"api/evaluations/{graph.Evaluation.Id}/submissions", Ct));
    }

    [Fact]
    public async Task GetMineByEvaluation_lists_the_callers_and_their_teams_submissions_to_a_member_holding_ViewTeam()
    {
        var graph = await Seed();
        graph.ScoringModel.UseUserScore = true;
        graph.ScoringModel.UseTeamScore = true;
        await Db.SaveChangesAsync(Ct);
        var actor = await Actor().OnTeam(graph.Team.Id, permissions: [TeamPermission.ViewTeam]).SeedAsync();
        var team = await SeedSubmission(graph, graph.Team.Id);
        var mine = await SeedSubmission(graph, graph.Team.Id, actor.Id);

        var submissions = await ReadAsync<List<Submission>>(await Client(actor).GetAsync($"api/evaluations/{graph.Evaluation.Id}/my-submissions", Ct));

        Assert.Equal(new[] { team.Submission.Id, mine.Submission.Id }.Order(), submissions.Select(x => x.Id).Order());
    }

    [Fact]
    public async Task GetMineByEvaluation_leaves_out_the_teams_submission_for_a_member_without_ViewTeam()
    {
        var graph = await Seed();
        graph.ScoringModel.UseUserScore = true;
        graph.ScoringModel.UseTeamScore = true;
        await Db.SaveChangesAsync(Ct);
        var actor = await Actor().OnTeam(graph.Team.Id, permissions: []).SeedAsync();
        await SeedSubmission(graph, graph.Team.Id);
        var mine = await SeedSubmission(graph, graph.Team.Id, actor.Id);

        var submissions = await ReadAsync<List<Submission>>(await Client(actor).GetAsync($"api/evaluations/{graph.Evaluation.Id}/my-submissions", Ct));

        Assert.Equal(mine.Submission.Id, Assert.Single(submissions).Id);
    }

    [Theory]
    [InlineData(TeamPermission.ViewPastOfficialScore, true, false)]
    [InlineData(TeamPermission.ViewCurrentOfficialScore, false, true)]
    [InlineData(TeamPermission.EditOfficialScore, false, true)]
    [InlineData(TeamPermission.ViewTeam, false, false)]
    public async Task GetMineByEvaluation_lists_the_official_submissions_the_members_team_permission_reads(TeamPermission permission, bool past, bool current)
    {
        var graph = await Seed();
        graph.ScoringModel.UseOfficialScore = true;
        await Db.SaveChangesAsync(Ct);
        var pastOfficial = await SeedSubmission(graph);
        await SeedMoveAndAdvance(graph, 1);
        var currentOfficial = await SeedSubmission(graph, moveNumber: 1);
        var actor = await Actor().OnTeam(graph.Team.Id, permissions: [permission]).SeedAsync();

        var submissions = await ReadAsync<List<Submission>>(await Client(actor).GetAsync($"api/evaluations/{graph.Evaluation.Id}/my-submissions", Ct));

        var ids = submissions.Select(x => x.Id).ToList();
        Assert.Equal((past, current), (ids.Contains(pastOfficial.Submission.Id), ids.Contains(currentOfficial.Submission.Id)));
    }

    [Fact]
    public async Task GetMineByEvaluation_answers_a_caller_on_none_of_its_teams_with_an_empty_list()
    {
        var graph = await Seed();
        await SeedSubmission(graph, graph.Team.Id);
        var actor = await Actor().OnEvaluation(graph.Evaluation.Id, permissions: [EvaluationPermission.ViewEvaluation]).SeedAsync();

        Assert.Empty(await ReadAsync<List<Submission>>(await Client(actor).GetAsync($"api/evaluations/{graph.Evaluation.Id}/my-submissions", Ct)));
    }

    [Fact]
    public async Task GetByEvaluationTeam_lists_the_teams_submissions_to_a_caller_holding_ViewTeam_on_it()
    {
        var graph = await Seed();
        var submission = await SeedSubmission(graph, graph.Team.Id);
        var actor = await Actor().OnTeam(graph.Team.Id, permissions: [TeamPermission.ViewTeam]).SeedAsync();

        var submissions = await ReadAsync<List<Submission>>(await Client(actor).GetAsync($"api/evaluations/{graph.Evaluation.Id}/teams/{graph.Team.Id}/submissions", Ct));

        Assert.Equal(submission.Submission.Id, Assert.Single(submissions).Id);
    }

    [Fact]
    public async Task GetByEvaluationTeam_lists_only_the_callers_own_user_submission_to_a_member_holding_ViewTeam()
    {
        var graph = await Seed();
        var teammate = TestData.User(name: "Teammate");
        await Seed(teammate);
        var team = await SeedSubmission(graph, graph.Team.Id);
        await SeedSubmission(graph, graph.Team.Id, teammate.Id);
        var actor = await Actor().OnTeam(graph.Team.Id, permissions: [TeamPermission.ViewTeam]).SeedAsync();
        var mine = await SeedSubmission(graph, graph.Team.Id, actor.Id);

        var submissions = await ReadAsync<List<Submission>>(await Client(actor).GetAsync($"api/evaluations/{graph.Evaluation.Id}/teams/{graph.Team.Id}/submissions", Ct));

        Assert.Equal(new[] { team.Submission.Id, mine.Submission.Id }.Order(), submissions.Select(x => x.Id).Order());
    }

    /// <summary>The team in the route belongs to another evaluation: nothing of either evaluation is returned.</summary>
    [Fact]
    public async Task GetByEvaluationTeam_answers_a_team_of_another_evaluation_with_not_found()
    {
        var graph = await Seed();
        var other = await Seed("Other");
        graph.ScoringModel.UseOfficialScore = true;
        await Db.SaveChangesAsync(Ct);
        await SeedSubmission(other);
        var actor = await Actor().OnTeam(graph.Team.Id, permissions: [TeamPermission.ViewTeam]).SeedAsync();

        await AssertApiError(HttpStatusCode.NotFound, await Client(actor).GetAsync($"api/evaluations/{other.Evaluation.Id}/teams/{graph.Team.Id}/submissions", Ct));
    }

    /// <summary>The team-type average depends only on ShowTeamTypeAverage, not on the official score contributor flag.</summary>
    [Fact]
    public async Task GetByEvaluationTeam_includes_the_team_type_average_for_a_team_type_that_is_not_an_official_score_contributor()
    {
        var graph = await Seed();
        graph.TeamType.ShowTeamTypeAverage = true;
        await Db.SaveChangesAsync(Ct);
        await SeedSubmission(graph, graph.Team.Id);
        var actor = await Actor().OnTeam(graph.Team.Id, permissions: [TeamPermission.ViewTeam]).SeedAsync();

        var submissions = await ReadAsync<List<Submission>>(await Client(actor).GetAsync($"api/evaluations/{graph.Evaluation.Id}/teams/{graph.Team.Id}/submissions", Ct));

        Assert.Contains(submissions, x => x.UserId == null && x.TeamId == null && x.GroupId == graph.TeamType.Id);
    }

    [Fact]
    public async Task GetByEvaluationTeam_lists_the_teams_submissions_to_a_caller_holding_ObserveEvaluation_on_the_evaluation()
    {
        var graph = await Seed();
        await SeedSubmission(graph, graph.Team.Id);
        var actor = await Actor().OnEvaluation(graph.Evaluation.Id, permissions: [EvaluationPermission.ObserveEvaluation]).SeedAsync();

        await AssertStatus(HttpStatusCode.OK, await Client(actor).GetAsync($"api/evaluations/{graph.Evaluation.Id}/teams/{graph.Team.Id}/submissions", Ct));
    }

    [Fact]
    public async Task GetByEvaluationTeam_lists_the_teams_submissions_to_a_caller_holding_ObserveEvaluations()
    {
        var graph = await Seed();
        var submission = await SeedSubmission(graph, graph.Team.Id);
        var actor = await Actor().WithSystemPermissions(SystemPermission.ObserveEvaluations).SeedAsync();

        var submissions = await ReadAsync<List<Submission>>(await Client(actor).GetAsync($"api/evaluations/{graph.Evaluation.Id}/teams/{graph.Team.Id}/submissions", Ct));

        Assert.Equal(submission.Submission.Id, Assert.Single(submissions).Id);
    }

    [Fact]
    public async Task GetByEvaluationTeam_is_forbidden_for_a_caller_holding_only_ExecuteEvaluations()
    {
        var graph = await Seed();
        var actor = await Actor().WithSystemPermissions(SystemPermission.ExecuteEvaluations).SeedAsync();

        await AssertApiError(HttpStatusCode.Forbidden, await Client(actor).GetAsync($"api/evaluations/{graph.Evaluation.Id}/teams/{graph.Team.Id}/submissions", Ct));
    }

    [Fact]
    public async Task GetByEvaluationTeam_is_forbidden_for_a_caller_holding_ViewTeam_only_on_a_sibling_team()
    {
        var graph = await Seed();
        var actor = await Actor().OnNewTeam(graph.Evaluation.Id, TeamPermission.ViewTeam).SeedAsync();

        await AssertStatus(HttpStatusCode.Forbidden, await Client(actor).GetAsync($"api/evaluations/{graph.Evaluation.Id}/teams/{graph.Team.Id}/submissions", Ct));
    }

    [Fact]
    public async Task GetByEvaluationTeam_is_forbidden_for_a_caller_holding_only_ViewEvaluation_on_the_evaluation()
    {
        var graph = await Seed();
        var actor = await Actor().OnEvaluation(graph.Evaluation.Id, permissions: [EvaluationPermission.ViewEvaluation]).SeedAsync();

        await AssertStatus(HttpStatusCode.Forbidden, await Client(actor).GetAsync($"api/evaluations/{graph.Evaluation.Id}/teams/{graph.Team.Id}/submissions", Ct));
    }

    [Fact]
    public async Task Get_returns_the_teams_submission_to_a_member_holding_ViewTeam()
    {
        var graph = await Seed();
        var submission = await SeedSubmission(graph, graph.Team.Id);
        var actor = await Actor().OnTeam(graph.Team.Id, permissions: [TeamPermission.ViewTeam]).SeedAsync();

        var read = await ReadAsync<Submission>(await Client(actor).GetAsync($"api/submissions/{submission.Submission.Id}", Ct));

        Assert.Equal(submission.Option.Id, Assert.Single(Assert.Single(read.SubmissionCategories).SubmissionOptions).Id);
    }

    [Theory]
    [InlineData(EvaluationPermission.ViewEvaluation)]
    [InlineData(EvaluationPermission.ObserveEvaluation)]
    public async Task Get_returns_the_submission_to_a_caller_holding_an_evaluation_permission_on_the_evaluation(EvaluationPermission permission)
    {
        var graph = await Seed();
        var submission = await SeedSubmission(graph, graph.Team.Id);
        var actor = await Actor().OnEvaluation(graph.Evaluation.Id, permissions: [permission]).SeedAsync();

        await AssertStatus(HttpStatusCode.OK, await Client(actor).GetAsync($"api/submissions/{submission.Submission.Id}", Ct));
    }

    [Theory]
    [InlineData(SystemPermission.ViewEvaluations)]
    [InlineData(SystemPermission.ObserveEvaluations)]
    public async Task Get_returns_the_submission_to_a_caller_holding_a_system_permission(SystemPermission permission)
    {
        var graph = await Seed();
        var submission = await SeedSubmission(graph, graph.Team.Id);
        var actor = await Actor().WithSystemPermissions(permission).SeedAsync();

        var read = await ReadAsync<Submission>(await Client(actor).GetAsync($"api/submissions/{submission.Submission.Id}", Ct));

        Assert.Equal(submission.Option.Id, Assert.Single(Assert.Single(read.SubmissionCategories).SubmissionOptions).Id);
    }

    [Fact]
    public async Task Get_is_forbidden_for_a_caller_holding_only_ExecuteEvaluations()
    {
        var graph = await Seed();
        var submission = await SeedSubmission(graph, graph.Team.Id);
        var actor = await Actor().WithSystemPermissions(SystemPermission.ExecuteEvaluations).SeedAsync();

        await AssertApiError(HttpStatusCode.Forbidden, await Client(actor).GetAsync($"api/submissions/{submission.Submission.Id}", Ct));
    }

    [Fact]
    public async Task Get_is_forbidden_for_a_member_holding_only_EditTeamScore()
    {
        var graph = await Seed();
        var submission = await SeedSubmission(graph, graph.Team.Id);
        var actor = await Actor().OnTeam(graph.Team.Id, permissions: [TeamPermission.EditTeamScore]).SeedAsync();

        await AssertStatus(HttpStatusCode.Forbidden, await Client(actor).GetAsync($"api/submissions/{submission.Submission.Id}", Ct));
    }

    [Fact]
    public async Task Get_is_forbidden_for_a_caller_holding_ViewTeam_only_on_a_team_in_another_evaluation()
    {
        var graph = await Seed();
        var other = await Seed("Other");
        var submission = await SeedSubmission(graph, graph.Team.Id);
        var actor = await Actor().OnNewTeam(other.Evaluation.Id, TeamPermission.ViewTeam).SeedAsync();

        await AssertApiError(HttpStatusCode.Forbidden, await Client(actor).GetAsync($"api/submissions/{submission.Submission.Id}", Ct));
    }

    [Fact]
    public async Task Get_is_forbidden_for_a_caller_holding_ViewEvaluation_only_on_another_evaluation()
    {
        var graph = await Seed();
        var submission = await SeedSubmission(graph, graph.Team.Id);
        var actor = await Actor().OnNewEvaluation(EvaluationPermission.ViewEvaluation).SeedAsync();

        await AssertApiError(HttpStatusCode.Forbidden, await Client(actor).GetAsync($"api/submissions/{submission.Submission.Id}", Ct));
    }

    [Fact]
    public async Task Get_is_forbidden_for_a_member_holding_ViewTeam_only_on_a_sibling_team()
    {
        var graph = await Seed();
        var submission = await SeedSubmission(graph, graph.Team.Id);
        var actor = await Actor().OnNewTeam(graph.Evaluation.Id, TeamPermission.ViewTeam).SeedAsync();

        await AssertApiError(HttpStatusCode.Forbidden, await Client(actor).GetAsync($"api/submissions/{submission.Submission.Id}", Ct));
    }

    [Fact]
    public async Task Get_returns_a_past_official_submission_to_a_member_holding_only_ViewPastOfficialScore()
    {
        var graph = await Seed();
        var past = await SeedSubmission(graph);
        await SeedMoveAndAdvance(graph, 1);
        var actor = await Actor().OnTeam(graph.Team.Id, permissions: [TeamPermission.ViewPastOfficialScore]).SeedAsync();

        Assert.Equal(past.Submission.Id, (await ReadAsync<Submission>(await Client(actor).GetAsync($"api/submissions/{past.Submission.Id}", Ct))).Id);
    }

    [Fact]
    public async Task Get_is_forbidden_on_a_past_official_submission_for_a_member_holding_only_ViewCurrentOfficialScore()
    {
        var graph = await Seed();
        var past = await SeedSubmission(graph);
        await SeedMoveAndAdvance(graph, 1);
        var actor = await Actor().OnTeam(graph.Team.Id, permissions: [TeamPermission.ViewCurrentOfficialScore]).SeedAsync();

        await AssertApiError(HttpStatusCode.Forbidden, await Client(actor).GetAsync($"api/submissions/{past.Submission.Id}", Ct));
    }

    [Fact]
    public async Task Get_is_forbidden_on_the_current_moves_official_submission_for_a_member_holding_only_ViewPastOfficialScore()
    {
        var graph = await Seed();
        var current = await SeedSubmission(graph);
        var actor = await Actor().OnTeam(graph.Team.Id, permissions: [TeamPermission.ViewPastOfficialScore]).SeedAsync();

        await AssertApiError(HttpStatusCode.Forbidden, await Client(actor).GetAsync($"api/submissions/{current.Submission.Id}", Ct));
    }

    [Fact]
    public async Task Get_is_forbidden_on_a_future_moves_official_submission_for_a_member_holding_ViewCurrentOfficialScore()
    {
        var graph = await Seed();
        await Seed(TestData.Move(graph.Evaluation.Id, 1));
        var future = await SeedSubmission(graph, moveNumber: 1);
        var actor = await Actor().OnTeam(graph.Team.Id, permissions: [TeamPermission.ViewCurrentOfficialScore, TeamPermission.ViewPastOfficialScore]).SeedAsync();

        await AssertApiError(HttpStatusCode.Forbidden, await Client(actor).GetAsync($"api/submissions/{future.Submission.Id}", Ct));
    }

    [Theory]
    [InlineData(TeamPermission.ViewCurrentOfficialScore)]
    [InlineData(TeamPermission.EditOfficialScore)]
    public async Task Get_returns_the_current_moves_official_submission_to_a_member_holding_a_current_official_score_permission(TeamPermission permission)
    {
        var graph = await Seed();
        var current = await SeedSubmission(graph);
        var actor = await Actor().OnTeam(graph.Team.Id, permissions: [permission]).SeedAsync();

        Assert.Equal(current.Submission.Id, (await ReadAsync<Submission>(await Client(actor).GetAsync($"api/submissions/{current.Submission.Id}", Ct))).Id);
    }

    /// <summary>The official submission belongs to the evaluation, so a permission held on any of the caller's teams in it counts.</summary>
    [Fact]
    public async Task Get_returns_the_current_moves_official_submission_to_a_member_holding_the_permission_on_a_second_team()
    {
        var graph = await Seed();
        var current = await SeedSubmission(graph);
        var actor = await Actor().OnTeam(graph.Team.Id, permissions: [TeamPermission.ViewTeam]).OnNewTeam(graph.Evaluation.Id, TeamPermission.ViewCurrentOfficialScore).SeedAsync();

        await AssertStatus(HttpStatusCode.OK, await Client(actor).GetAsync($"api/submissions/{current.Submission.Id}", Ct));
    }

    [Fact]
    public async Task Get_answers_an_unknown_submission_with_not_found_for_a_team_member()
    {
        var graph = await Seed();
        var actor = await Actor().OnTeam(graph.Team.Id, permissions: [TeamPermission.ViewTeam]).SeedAsync();

        await AssertApiError(HttpStatusCode.NotFound, await Client(actor).GetAsync($"api/submissions/{Guid.NewGuid()}", Ct));
    }

    [Fact]
    public async Task Create_builds_a_submission_with_a_category_and_option_per_scoring_one_for_a_caller_holding_ManageEvaluation()
    {
        var graph = await Seed();
        var actor = await Actor().OnEvaluation(graph.Evaluation.Id, permissions: [EvaluationPermission.ManageEvaluation]).SeedAsync();

        var created = await ReadAsync<Submission>(await Client(actor).PostAsJsonAsync("api/submissions", NewSubmission(graph, 1), Ct));

        await using var context = NewContext();
        var stored = await context.Submissions.Include(x => x.SubmissionCategories).ThenInclude(x => x.SubmissionOptions).SingleAsync(x => x.Id == created.Id, Ct);
        Assert.Equal(graph.Option.Id, Assert.Single(Assert.Single(stored.SubmissionCategories).SubmissionOptions).ScoringOptionId);
    }

    [Theory]
    [InlineData("scoringModel")]
    [InlineData("team")]
    public async Task Create_answers_a_reference_outside_the_evaluation_with_conflict(string reference)
    {
        var graph = await Seed();
        var other = await Seed("Other");
        var body = reference == "team"
            ? NewSubmission(graph, 1) with { TeamId = other.Team.Id }
            : NewSubmission(graph, 1) with { ScoringModelId = other.ScoringModel.Id };

        await AssertApiError(HttpStatusCode.Conflict, await RootClient.PostAsJsonAsync("api/submissions", body, Ct));

        await using var context = NewContext();
        Assert.False(await context.Submissions.AnyAsync(x => x.MoveNumber == 1, Ct));
    }

    [Fact]
    public async Task Create_broadcasts_the_teams_submission_to_the_team()
    {
        var graph = await Seed();

        var created = await ReadAsync<Submission>(await RootClient.PostAsJsonAsync("api/submissions", NewSubmission(graph, 1), Ct));

        var broadcast = Assert.Single(Factory.Hub<MainHub>().ToGroup(graph.Team.Id), x => x.Method == MainHubMethods.SubmissionCreated);
        Assert.Equal(created.Id, Assert.IsType<Submission>(broadcast.Arguments[0]).Id);
    }

    [Fact]
    public async Task Create_is_forbidden_for_a_caller_holding_only_EditEvaluation()
    {
        var graph = await Seed();
        var actor = await Actor().OnEvaluation(graph.Evaluation.Id, permissions: [EvaluationPermission.EditEvaluation]).SeedAsync();

        await AssertStatus(HttpStatusCode.Forbidden, await Client(actor).PostAsJsonAsync("api/submissions", NewSubmission(graph, 1), Ct));

        await using var context = NewContext();
        Assert.False(await context.Submissions.AnyAsync(Ct));
    }

    [Fact]
    public async Task Create_builds_a_submission_for_a_caller_holding_ManageEvaluations()
    {
        var graph = await Seed();
        var actor = await Actor().WithSystemPermissions(SystemPermission.ManageEvaluations).SeedAsync();

        var created = await ReadAsync<Submission>(await Client(actor).PostAsJsonAsync("api/submissions", NewSubmission(graph, 1), Ct));

        await using var context = NewContext();
        var stored = await context.Submissions.SingleAsync(x => x.Id == created.Id, Ct);
        Assert.Equal(graph.Team.Id, stored.TeamId);
        Assert.Equal(1, stored.MoveNumber);
    }

    [Fact]
    public async Task Create_is_forbidden_for_a_caller_holding_only_EditEvaluations()
    {
        var graph = await Seed();
        var actor = await Actor().WithSystemPermissions(SystemPermission.EditEvaluations).SeedAsync();

        await AssertApiError(HttpStatusCode.Forbidden, await Client(actor).PostAsJsonAsync("api/submissions", NewSubmission(graph, 1), Ct));

        await using var context = NewContext();
        Assert.False(await context.Submissions.AnyAsync(Ct));
    }

    /// <summary>A second team submission for the same move is answered with a 500.</summary>
    [Fact]
    public async Task Create_answers_a_duplicate_submission_with_a_server_error()
    {
        var graph = await Seed();
        await SeedSubmission(graph, graph.Team.Id, moveNumber: 1);

        var error = await AssertApiError(HttpStatusCode.InternalServerError, await RootClient.PostAsJsonAsync("api/submissions", NewSubmission(graph, 1), Ct));
        Assert.StartsWith("An error occurred while saving the entity changes.", error.Detail);
    }

    /// <summary>A body without an evaluation id is refused with a 500.</summary>
    [Fact]
    public async Task Create_answers_a_body_without_an_evaluation_with_a_server_error()
    {
        var graph = await Seed();

        var error = await AssertApiError(HttpStatusCode.InternalServerError, await RootClient.PostAsJsonAsync("api/submissions", NewSubmission(graph, 1) with { EvaluationId = Guid.Empty }, Ct));

        Assert.Equal("An Evaluation ID must be supplied to create a new submission", error.Detail);
    }

    [Fact]
    public async Task Update_completes_the_teams_submission_for_a_member_holding_SubmitTeamScore()
    {
        var graph = await Seed();
        var submission = await SeedSubmission(graph, graph.Team.Id);
        var actor = await Actor().OnTeam(graph.Team.Id, permissions: [TeamPermission.SubmitTeamScore]).SeedAsync();

        var response = await Client(actor).PutAsJsonAsync($"api/submissions/{submission.Submission.Id}", Body(submission, graph) with { Status = "Complete" }, Ct);

        await AssertStatus(HttpStatusCode.OK, response);
        await using var context = NewContext();
        Assert.Equal(ItemStatus.Complete, (await context.Submissions.SingleAsync(x => x.Id == submission.Submission.Id, Ct)).Status);
    }

    [Fact]
    public async Task Update_is_forbidden_for_a_member_holding_only_EditTeamScore()
    {
        var graph = await Seed();
        var submission = await SeedSubmission(graph, graph.Team.Id);
        var actor = await Actor().OnTeam(graph.Team.Id, permissions: [TeamPermission.EditTeamScore]).SeedAsync();

        await AssertStatus(HttpStatusCode.Forbidden, await Client(actor).PutAsJsonAsync($"api/submissions/{submission.Submission.Id}", Body(submission, graph) with { Status = "Complete" }, Ct));

        await using var context = NewContext();
        Assert.Equal(ItemStatus.Active, (await context.Submissions.SingleAsync(x => x.Id == submission.Submission.Id, Ct)).Status);
    }

    /// <summary>The body's score, place and scoresheet are ignored; only the status is applied.</summary>
    [Fact]
    public async Task Update_applies_only_the_status_whatever_else_the_body_sends()
    {
        var graph = await Seed();
        var other = await TestScenario.SeedEvaluationAsync(Db, Ct, "Other Evaluation");
        var submission = await SeedSubmission(graph, graph.Team.Id);
        var victim = await SeedSubmission(other, other.Team.Id);
        var actor = await Actor().OnTeam(graph.Team.Id, permissions: [TeamPermission.SubmitTeamScore]).SeedAsync();
        var before = await ReadBack(c => c.Submissions.AsNoTracking().SingleAsync(x => x.Id == submission.Submission.Id, Ct));

        var response = await Client(actor).PutAsJsonAsync($"api/submissions/{submission.Submission.Id}", new
        {
            id = submission.Submission.Id,
            status = "Complete",
            score = 99.5,
            evaluationId = other.Evaluation.Id,
            teamId = other.Team.Id,
            userId = actor.Id,
            scoringModelId = other.ScoringModel.Id,
            moveNumber = 7,
            submissionCategories = new[] { new { id = victim.Category.Id, submissionId = submission.Submission.Id, scoringCategoryId = other.Category.Id, score = 42.0 } },
        }, Ct);

        await AssertStatus(HttpStatusCode.OK, response);
        await using var context = NewContext();
        var stored = await context.Submissions.SingleAsync(x => x.Id == submission.Submission.Id, Ct);
        Assert.Equal(
            (ItemStatus.Complete, before.Score, graph.Evaluation.Id, (Guid?)graph.Team.Id, (Guid?)null, graph.ScoringModel.Id, before.MoveNumber),
            (stored.Status, stored.Score, stored.EvaluationId.Value, stored.TeamId, stored.UserId, stored.ScoringModelId, stored.MoveNumber));
        var victimCategory = await context.SubmissionCategories.SingleAsync(x => x.Id == victim.Category.Id, Ct);
        Assert.Equal((victim.Submission.Id, victim.Category.Score), (victimCategory.SubmissionId, victimCategory.Score));
    }

    [Fact]
    public async Task Update_answers_a_body_without_a_status_with_bad_request()
    {
        var graph = await Seed();
        var submission = await SeedSubmission(graph, graph.Team.Id);
        var actor = await Actor().OnTeam(graph.Team.Id, permissions: [TeamPermission.SubmitTeamScore]).SeedAsync();

        await AssertProblem(HttpStatusCode.BadRequest, await Client(actor).PutAsJsonAsync($"api/submissions/{submission.Submission.Id}", new { score = 99.5 }, Ct));

        await using var context = NewContext();
        Assert.Equal(ItemStatus.Active, (await context.Submissions.SingleAsync(x => x.Id == submission.Submission.Id, Ct)).Status);
    }

    [Fact]
    public async Task Delete_removes_the_submission_for_a_caller_holding_ManageEvaluation_on_its_evaluation()
    {
        var graph = await Seed();
        var submission = await SeedSubmission(graph, graph.Team.Id);
        var actor = await Actor().OnEvaluation(graph.Evaluation.Id, permissions: [EvaluationPermission.ManageEvaluation]).SeedAsync();

        await AssertStatus(HttpStatusCode.NoContent, await Client(actor).DeleteAsync($"api/submissions/{submission.Submission.Id}", Ct));

        await using var context = NewContext();
        Assert.False(await context.Submissions.AnyAsync(x => x.Id == submission.Submission.Id, Ct));
        Assert.False(await context.SubmissionOptions.AnyAsync(x => x.Id == submission.Option.Id, Ct));
    }

    [Fact]
    public async Task Delete_removes_the_submission_for_a_caller_holding_ManageEvaluations()
    {
        var graph = await Seed();
        var submission = await SeedSubmission(graph, graph.Team.Id);
        var actor = await Actor().WithSystemPermissions(SystemPermission.ManageEvaluations).SeedAsync();

        await AssertStatus(HttpStatusCode.NoContent, await Client(actor).DeleteAsync($"api/submissions/{submission.Submission.Id}", Ct));

        await using var context = NewContext();
        Assert.False(await context.Submissions.AnyAsync(x => x.Id == submission.Submission.Id, Ct));
        Assert.False(await context.SubmissionOptions.AnyAsync(x => x.Id == submission.Option.Id, Ct));
    }

    [Fact]
    public async Task Delete_is_forbidden_for_a_caller_holding_only_EditEvaluations()
    {
        var graph = await Seed();
        var submission = await SeedSubmission(graph, graph.Team.Id);
        var actor = await Actor().WithSystemPermissions(SystemPermission.EditEvaluations).SeedAsync();

        await AssertApiError(HttpStatusCode.Forbidden, await Client(actor).DeleteAsync($"api/submissions/{submission.Submission.Id}", Ct));

        await using var context = NewContext();
        Assert.True(await context.Submissions.AnyAsync(x => x.Id == submission.Submission.Id, Ct));
    }

    [Fact]
    public async Task Delete_is_forbidden_for_a_caller_holding_ManageEvaluation_only_on_another_evaluation()
    {
        var graph = await Seed();
        var submission = await SeedSubmission(graph, graph.Team.Id);
        var actor = await Actor().OnNewEvaluation(EvaluationPermission.ManageEvaluation).SeedAsync();

        await AssertApiError(HttpStatusCode.Forbidden, await Client(actor).DeleteAsync($"api/submissions/{submission.Submission.Id}", Ct));

        await using var context = NewContext();
        Assert.True(await context.Submissions.AnyAsync(x => x.Id == submission.Submission.Id, Ct));
    }

    [Fact]
    public async Task Delete_is_forbidden_for_a_member_holding_SubmitTeamScore()
    {
        var graph = await Seed();
        var submission = await SeedSubmission(graph, graph.Team.Id);
        var actor = await Actor().OnTeam(graph.Team.Id, permissions: [TeamPermission.SubmitTeamScore]).SeedAsync();

        await AssertStatus(HttpStatusCode.Forbidden, await Client(actor).DeleteAsync($"api/submissions/{submission.Submission.Id}", Ct));

        await using var context = NewContext();
        Assert.True(await context.Submissions.AnyAsync(x => x.Id == submission.Submission.Id, Ct));
    }

    [Fact]
    public async Task ClearSubmission_deselects_every_option_for_a_member_holding_EditTeamScore()
    {
        var graph = await Seed();
        var submission = await SeedSubmission(graph, graph.Team.Id);
        submission.Option.IsSelected = true;
        await Db.SaveChangesAsync(Ct);
        var actor = await Actor().OnTeam(graph.Team.Id, permissions: [TeamPermission.EditTeamScore]).SeedAsync();

        await AssertStatus(HttpStatusCode.OK, await Client(actor).PutAsync($"api/submissions/{submission.Submission.Id}/clear", null, Ct));

        await using var context = NewContext();
        Assert.False((await context.SubmissionOptions.SingleAsync(x => x.Id == submission.Option.Id, Ct)).IsSelected);
    }

    [Fact]
    public async Task ClearSubmission_is_forbidden_for_a_member_holding_only_ViewTeam()
    {
        var graph = await Seed();
        var submission = await SeedSubmission(graph, graph.Team.Id);
        submission.Option.IsSelected = true;
        await Db.SaveChangesAsync(Ct);
        var actor = await Actor().OnTeam(graph.Team.Id, permissions: [TeamPermission.ViewTeam]).SeedAsync();

        await AssertStatus(HttpStatusCode.Forbidden, await Client(actor).PutAsync($"api/submissions/{submission.Submission.Id}/clear", null, Ct));

        await using var context = NewContext();
        Assert.True((await context.SubmissionOptions.SingleAsync(x => x.Id == submission.Option.Id, Ct)).IsSelected);
    }

    /// <summary>Clearing a submission that is no longer active is refused with a 500.</summary>
    [Fact]
    public async Task ClearSubmission_answers_a_completed_submission_with_a_server_error()
    {
        var graph = await Seed();
        var submission = await SeedSubmission(graph, graph.Team.Id);
        submission.Submission.Status = ItemStatus.Complete;
        await Db.SaveChangesAsync(Ct);
        var actor = await Actor().OnTeam(graph.Team.Id, permissions: [TeamPermission.EditTeamScore]).SeedAsync();

        var error = await AssertApiError(HttpStatusCode.InternalServerError, await Client(actor).PutAsync($"api/submissions/{submission.Submission.Id}/clear", null, Ct));
        Assert.StartsWith("Cannot clear selections of a submission", error.Detail);
    }

    [Fact]
    public async Task PresetSubmission_copies_the_previous_moves_selections_for_a_member_holding_EditTeamScore()
    {
        var graph = await Seed();
        var previous = await SeedSubmission(graph, graph.Team.Id);
        previous.Option.IsSelected = true;
        await Db.SaveChangesAsync(Ct);
        var target = await SeedSubmission(graph, graph.Team.Id, moveNumber: 1);
        var actor = await Actor().OnTeam(graph.Team.Id, permissions: [TeamPermission.EditTeamScore]).SeedAsync();

        await AssertStatus(HttpStatusCode.OK, await Client(actor).PutAsync($"api/submissions/{target.Submission.Id}/preset", null, Ct));

        await using var context = NewContext();
        Assert.True((await context.SubmissionOptions.SingleAsync(x => x.Id == target.Option.Id, Ct)).IsSelected);
    }

    [Fact]
    public async Task PresetSubmission_is_forbidden_for_a_member_holding_EditTeamScore_only_in_another_evaluation()
    {
        var graph = await Seed();
        var other = await Seed("Other");
        var target = await SeedSubmission(graph, graph.Team.Id, moveNumber: 1);
        var actor = await Actor().OnNewTeam(other.Evaluation.Id, TeamPermission.EditTeamScore).SeedAsync();

        await AssertStatus(HttpStatusCode.Forbidden, await Client(actor).PutAsync($"api/submissions/{target.Submission.Id}/preset", null, Ct));
    }

    [Fact]
    public async Task AddSubmissionComment_stores_the_comment_for_a_member_holding_EditTeamScore()
    {
        var graph = await Seed();
        var submission = await SeedSubmission(graph, graph.Team.Id);
        var actor = await Actor().OnTeam(graph.Team.Id, permissions: [TeamPermission.EditTeamScore]).SeedAsync();

        var response = await Client(actor).PostAsJsonAsync($"api/submissions/{submission.Submission.Id}/comments",
            new { submissionOptionId = submission.Option.Id, comment = "Because" }, Ct);

        await AssertStatus(HttpStatusCode.OK, response);
        await using var context = NewContext();
        Assert.Equal("Because", (await context.SubmissionComments.SingleAsync(x => x.SubmissionOptionId == submission.Option.Id, Ct)).Comment);
    }

    [Fact]
    public async Task AddSubmissionComment_is_forbidden_for_a_member_holding_only_ViewTeam()
    {
        var graph = await Seed();
        var submission = await SeedSubmission(graph, graph.Team.Id);
        var actor = await Actor().OnTeam(graph.Team.Id, permissions: [TeamPermission.ViewTeam]).SeedAsync();

        await AssertStatus(HttpStatusCode.Forbidden, await Client(actor).PostAsJsonAsync($"api/submissions/{submission.Submission.Id}/comments",
            new { submissionOptionId = submission.Option.Id, comment = "Because" }, Ct));

        await using var context = NewContext();
        Assert.False(await context.SubmissionComments.AnyAsync(Ct));
    }

    [Fact]
    public async Task ChangeSubmissionComment_changes_the_comment_for_a_member_holding_EditTeamScore()
    {
        var graph = await Seed();
        var submission = await SeedSubmission(graph, graph.Team.Id);
        var comment = TestData.SubmissionComment(submission.Option.Id, "Before");
        await Seed(comment);
        var actor = await Actor().OnTeam(graph.Team.Id, permissions: [TeamPermission.EditTeamScore]).SeedAsync();

        var response = await Client(actor).PutAsJsonAsync($"api/submissions/{submission.Submission.Id}/comments/{comment.Id}",
            new { id = comment.Id, submissionOptionId = submission.Option.Id, comment = "After" }, Ct);

        await AssertStatus(HttpStatusCode.OK, response);
        await using var context = NewContext();
        Assert.Equal("After", (await context.SubmissionComments.SingleAsync(x => x.Id == comment.Id, Ct)).Comment);
    }

    [Fact]
    public async Task ChangeSubmissionComment_is_forbidden_for_a_member_holding_only_ViewTeam()
    {
        var graph = await Seed();
        var submission = await SeedSubmission(graph, graph.Team.Id);
        var comment = TestData.SubmissionComment(submission.Option.Id, "Before");
        await Seed(comment);
        var actor = await Actor().OnTeam(graph.Team.Id, permissions: [TeamPermission.ViewTeam]).SeedAsync();

        await AssertStatus(HttpStatusCode.Forbidden, await Client(actor).PutAsJsonAsync($"api/submissions/{submission.Submission.Id}/comments/{comment.Id}",
            new { id = comment.Id, submissionOptionId = submission.Option.Id, comment = "After" }, Ct));

        await using var context = NewContext();
        Assert.Equal("Before", (await context.SubmissionComments.SingleAsync(x => x.Id == comment.Id, Ct)).Comment);
    }

    [Fact]
    public async Task ChangeSubmissionComment_answers_a_comment_of_another_submission_with_not_found()
    {
        var graph = await Seed();
        var other = await Seed("Other");
        var mine = await SeedSubmission(graph, graph.Team.Id);
        var theirs = await SeedSubmission(other, other.Team.Id);
        var comment = TestData.SubmissionComment(theirs.Option.Id, "Theirs");
        await Seed(comment);
        var actor = await Actor().OnTeam(graph.Team.Id, permissions: [TeamPermission.EditTeamScore]).SeedAsync();

        var response = await Client(actor).PutAsJsonAsync($"api/submissions/{mine.Submission.Id}/comments/{comment.Id}",
            new { id = comment.Id, submissionOptionId = theirs.Option.Id, comment = "Overwritten" }, Ct);

        await AssertApiError(HttpStatusCode.NotFound, response);
        await using var context = NewContext();
        Assert.Equal("Theirs", (await context.SubmissionComments.SingleAsync(x => x.Id == comment.Id, Ct)).Comment);
    }

    [Fact]
    public async Task RemoveSubmissionComment_answers_a_comment_of_another_submission_with_not_found()
    {
        var graph = await Seed();
        var other = await Seed("Other");
        var mine = await SeedSubmission(graph, graph.Team.Id);
        var theirs = await SeedSubmission(other, other.Team.Id);
        var comment = TestData.SubmissionComment(theirs.Option.Id, "Theirs");
        await Seed(comment);
        var actor = await Actor().OnTeam(graph.Team.Id, permissions: [TeamPermission.EditTeamScore]).SeedAsync();

        await AssertApiError(HttpStatusCode.NotFound, await Client(actor).DeleteAsync($"api/submissions/{mine.Submission.Id}/comments/{comment.Id}", Ct));

        await using var context = NewContext();
        Assert.True(await context.SubmissionComments.AnyAsync(x => x.Id == comment.Id, Ct));
    }

    [Fact]
    public async Task AddSubmissionComment_answers_an_option_of_another_submission_with_conflict()
    {
        var graph = await Seed();
        var other = await Seed("Other");
        var mine = await SeedSubmission(graph, graph.Team.Id);
        var theirs = await SeedSubmission(other, other.Team.Id);
        var actor = await Actor().OnTeam(graph.Team.Id, permissions: [TeamPermission.EditTeamScore]).SeedAsync();

        await AssertApiError(HttpStatusCode.Conflict, await Client(actor).PostAsJsonAsync($"api/submissions/{mine.Submission.Id}/Comments",
            new { submissionOptionId = theirs.Option.Id, comment = "Planted" }, Ct));

        await using var context = NewContext();
        Assert.False(await context.SubmissionComments.AnyAsync(x => x.SubmissionOptionId == theirs.Option.Id, Ct));
    }

    [Fact]
    public async Task AddSubmissionComment_ignores_a_comment_id_in_the_body()
    {
        var graph = await Seed();
        var mine = await SeedSubmission(graph, graph.Team.Id);
        var actor = await Actor().OnTeam(graph.Team.Id, permissions: [TeamPermission.EditTeamScore]).SeedAsync();
        var chosenId = Guid.NewGuid();

        await AssertStatus(HttpStatusCode.OK, await Client(actor).PostAsJsonAsync($"api/submissions/{mine.Submission.Id}/Comments",
            new { id = chosenId, submissionOptionId = mine.Option.Id, comment = "Note" }, Ct));

        await using var context = NewContext();
        Assert.False(await context.SubmissionComments.AnyAsync(x => x.Id == chosenId, Ct));
        Assert.Equal("Note", (await context.SubmissionComments.SingleAsync(x => x.SubmissionOptionId == mine.Option.Id, Ct)).Comment);
    }

    [Fact]
    public async Task RemoveSubmissionComment_deletes_the_comment_for_a_member_holding_EditTeamScore()
    {
        var graph = await Seed();
        var submission = await SeedSubmission(graph, graph.Team.Id);
        var comment = TestData.SubmissionComment(submission.Option.Id);
        await Seed(comment);
        var actor = await Actor().OnTeam(graph.Team.Id, permissions: [TeamPermission.EditTeamScore]).SeedAsync();

        await AssertStatus(HttpStatusCode.OK, await Client(actor).DeleteAsync($"api/submissions/{submission.Submission.Id}/comments/{comment.Id}", Ct));

        await using var context = NewContext();
        Assert.False(await context.SubmissionComments.AnyAsync(x => x.Id == comment.Id, Ct));
    }

    [Fact]
    public async Task RemoveSubmissionComment_is_forbidden_for_a_member_holding_only_ViewTeam()
    {
        var graph = await Seed();
        var submission = await SeedSubmission(graph, graph.Team.Id);
        var comment = TestData.SubmissionComment(submission.Option.Id);
        await Seed(comment);
        var actor = await Actor().OnTeam(graph.Team.Id, permissions: [TeamPermission.ViewTeam]).SeedAsync();

        await AssertStatus(HttpStatusCode.Forbidden, await Client(actor).DeleteAsync($"api/submissions/{submission.Submission.Id}/comments/{comment.Id}", Ct));

        await using var context = NewContext();
        Assert.True(await context.SubmissionComments.AnyAsync(x => x.Id == comment.Id, Ct));
    }

    /// <summary>An official submission (no team, no user) is submitted with the team permission EditOfficialScore.</summary>
    [Fact]
    public async Task Update_completes_the_official_submission_for_a_member_holding_EditOfficialScore()
    {
        var graph = await Seed();
        var official = await SeedSubmission(graph);
        var actor = await Actor().OnTeam(graph.Team.Id, permissions: [TeamPermission.EditOfficialScore]).SeedAsync();

        var response = await Client(actor).PutAsJsonAsync($"api/submissions/{official.Submission.Id}", Body(official, graph) with { Status = "Complete" }, Ct);

        await AssertStatus(HttpStatusCode.OK, response);
        await using var context = NewContext();
        Assert.Equal(ItemStatus.Complete, (await context.Submissions.SingleAsync(x => x.Id == official.Submission.Id, Ct)).Status);
    }

    /// <summary>SubmitTeamScore, which submits the team's own submission, does not submit the official one.</summary>
    [Fact]
    public async Task Update_is_forbidden_on_the_official_submission_for_a_member_holding_only_SubmitTeamScore()
    {
        var graph = await Seed();
        var official = await SeedSubmission(graph);
        var actor = await Actor().OnTeam(graph.Team.Id, permissions: [TeamPermission.SubmitTeamScore]).SeedAsync();

        await AssertStatus(HttpStatusCode.Forbidden, await Client(actor).PutAsJsonAsync($"api/submissions/{official.Submission.Id}", Body(official, graph) with { Status = "Complete" }, Ct));

        await using var context = NewContext();
        Assert.Equal(ItemStatus.Active, (await context.Submissions.SingleAsync(x => x.Id == official.Submission.Id, Ct)).Status);
    }

    [Fact]
    public async Task Update_is_forbidden_on_the_official_submission_for_a_member_holding_EditOfficialScore_only_in_another_evaluation()
    {
        var graph = await Seed();
        var official = await SeedSubmission(graph);
        var other = await Seed("Other Evaluation");
        var actor = await Actor().OnNewTeam(other.Evaluation.Id, TeamPermission.EditOfficialScore).SeedAsync();

        await AssertStatus(HttpStatusCode.Forbidden, await Client(actor).PutAsJsonAsync($"api/submissions/{official.Submission.Id}", Body(official, graph) with { Status = "Complete" }, Ct));

        await using var context = NewContext();
        Assert.Equal(ItemStatus.Active, (await context.Submissions.SingleAsync(x => x.Id == official.Submission.Id, Ct)).Status);
    }

    private Task<EvaluationGraph> Seed(string description = "Test Evaluation") =>
        TestScenario.SeedEvaluationAsync(Db, Ct, description);

    private Task<SubmissionGraph> SeedSubmission(EvaluationGraph graph, Guid? teamId = null, Guid? userId = null, int moveNumber = 0) =>
        TestScenario.SeedSubmissionAsync(Db, Ct, graph, teamId, userId, moveNumber);

    private async Task SeedMoveAndAdvance(EvaluationGraph graph, int moveNumber)
    {
        await Seed(TestData.Move(graph.Evaluation.Id, moveNumber));
        graph.Evaluation.CurrentMoveNumber = moveNumber;
        await Db.SaveChangesAsync(Ct);
    }

    private static SubmissionBody NewSubmission(EvaluationGraph graph, int moveNumber) =>
        new(Guid.Empty, 0, "Active", graph.ScoringModel.Id, null, graph.Evaluation.Id, graph.Team.Id, moveNumber);

    private static SubmissionBody Body(SubmissionGraph submission, EvaluationGraph graph) =>
        new(submission.Submission.Id, 0, "Active", graph.ScoringModel.Id, submission.Submission.UserId, graph.Evaluation.Id, submission.Submission.TeamId, submission.Submission.MoveNumber);

    private sealed record SubmissionBody(Guid Id, double Score, string Status, Guid ScoringModelId, Guid? UserId, Guid EvaluationId, Guid? TeamId, int MoveNumber);
}
