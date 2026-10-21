// Copyright 2026 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.

using Cite.Api.Data.Models;
using Cite.Api.Infrastructure.Authorization;
using Cite.Api.Infrastructure.Options;
using Cite.Api.Services;
using Cite.Api.Tests.Support;
using Microsoft.Extensions.Logging.Abstractions;
using SubmissionComment = Cite.Api.ViewModels.SubmissionComment;

namespace Cite.Api.Tests.Services;

/// <summary>
/// <see cref="SubmissionService"/>'s xAPI statements for Scoresheet comments and submissions, which the hosted
/// application never builds because xAPI is off in appsettings.json. The xAPI service is a substitute this
/// test owns, reporting itself configured, and the statement is read off the call.
/// </summary>
public class SubmissionServiceTests(DatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    private readonly IXApiService _xApi = Substitute.For<IXApiService>();
    private Uri _verb;
    private Dictionary<string, string> _activity;
    private List<Dictionary<string, string>> _grouping;

    [Fact]
    public async Task AddCommentAsync_states_the_comment_as_the_result_on_its_scoring_option()
    {
        var (graph, submission, user) = await SeedUserSubmission();
        CaptureStatement();

        await using var context = NewContext();
        await Service(context, user).AddCommentAsync(submission.Submission.Id,
            new SubmissionComment { Comment = "Saw lateral movement", SubmissionOptionId = submission.Option.Id }, Ct);

        Assert.Equal("https://w3id.org/xapi/dod-isd/verbs/stated", _verb.ToString());
        Assert.Equal((graph.Option.Id.ToString(), "scoringOption", "Saw lateral movement"),
            (_activity["id"], _activity["type"], _activity["result"]));
    }

    [Fact]
    public async Task UpdateCommentAsync_logs_the_edited_comment_as_the_result()
    {
        var (_, submission, user) = await SeedUserSubmission();
        var comment = TestData.SubmissionComment(submission.Option.Id, "First draft");
        await Seed(comment);
        CaptureStatement();

        await using var context = NewContext();
        await Service(context, user).UpdateCommentAsync(submission.Submission.Id, comment.Id,
            new SubmissionComment { Id = comment.Id, Comment = "Revised", SubmissionOptionId = submission.Option.Id }, Ct);

        Assert.Equal("https://w3id.org/xapi/dod-isd/verbs/edited", _verb.ToString());
        Assert.Equal("Revised", _activity["result"]);
    }

    /// <summary>The comment is gone by the time the statement is built, so its text is read before the delete.</summary>
    [Fact]
    public async Task DeleteCommentAsync_logs_the_removed_comment_as_the_result()
    {
        var (_, submission, user) = await SeedUserSubmission();
        var comment = TestData.SubmissionComment(submission.Option.Id, "Remove me");
        await Seed(comment);
        CaptureStatement();

        await using var context = NewContext();
        await Service(context, user).DeleteCommentAsync(submission.Submission.Id, comment.Id, Ct);

        Assert.Equal("https://w3id.org/xapi/dod-isd/verbs/deleted", _verb.ToString());
        Assert.Equal("Remove me", _activity["result"]);
    }

    /// <summary>Selecting an option is not a comment, so its statement carries no result.</summary>
    [Fact]
    public async Task LogXApiAsync_for_an_option_without_a_response_carries_no_result()
    {
        var (_, submission, user) = await SeedUserSubmission();
        CaptureStatement();

        await using var context = NewContext();
        var option = TestMapper.Mapper.Map<Cite.Api.ViewModels.SubmissionOption>(submission.Option);
        await Service(context, user).LogXApiAsync(new Uri("https://w3id.org/xapi/dod-isd/verbs/edited"), null, option, Ct);

        Assert.False(_activity.ContainsKey("result"));
    }

    [Fact]
    public async Task LogCommentXApiAsync_sends_nothing_when_xapi_is_not_configured()
    {
        var (_, submission, user) = await SeedUserSubmission();
        _xApi.IsConfigured().Returns(false);

        await using var context = NewContext();
        var sent = await Service(context, user).LogCommentXApiAsync(
            new Uri("https://w3id.org/xapi/dod-isd/verbs/stated"), submission.Option.Id, "Unsent", Ct);

        Assert.False(sent);
        await _xApi.DidNotReceiveWithAnyArgs().CreateAsync(default, default, default, default, default, default, default, Ct);
    }

    /// <summary>A submission's activity is named by its scope (user, team or official) and its move.</summary>
    [Theory]
    [InlineData("User")]
    [InlineData("Team")]
    [InlineData("Official")]
    public async Task LogXApiAsync_names_a_submission_by_scope_and_move(string scope)
    {
        var graph = await TestScenario.SeedEvaluationAsync(Db, Ct);
        var user = TestData.User();
        await Seed(user, TestData.TeamMembership(graph.Team.Id, user.Id));
        var submission = await TestScenario.SeedSubmissionAsync(Db, Ct, graph,
            teamId: scope == "Official" ? null : graph.Team.Id,
            userId: scope == "User" ? user.Id : null);
        CaptureStatement();

        await using var context = NewContext();
        var viewModel = TestMapper.Mapper.Map<Cite.Api.ViewModels.Submission>(submission.Submission);
        await Service(context, user).LogXApiAsync(new Uri("https://w3id.org/xapi/dod-isd/verbs/submitted"), viewModel, null, Ct);

        Assert.Equal($"{scope} Submission — Move 0: {graph.Move.Description}", _activity["name"]);
        Assert.Equal("submission", _activity["type"]);
    }

    /// <summary>
    /// A submission is named and grouped by its own evaluation's move, not another evaluation's move with the
    /// same number (seeded first, so an unscoped lookup would find it).
    /// </summary>
    [Fact]
    public async Task LogXApiAsync_uses_the_submissions_own_evaluations_move()
    {
        var other = await TestScenario.SeedEvaluationAsync(Db, Ct, "Other");
        await Seed(TestData.Move(other.Evaluation.Id, 2, "Other Evaluation's Move"));
        var graph = await TestScenario.SeedEvaluationAsync(Db, Ct);
        await Seed(TestData.Move(graph.Evaluation.Id, 2, "This Evaluation's Move"));
        var (submission, user) = await SeedUserSubmissionAt(graph, 2);
        CaptureStatement();

        await using var context = NewContext();
        await Service(context, user).LogXApiAsync(new Uri("https://w3id.org/xapi/dod-isd/verbs/submitted"), submission, null, Ct);

        Assert.Equal("User Submission — Move 2: This Evaluation's Move", _activity["name"]);
        Assert.Equal("This Evaluation's Move", _grouping[0]["description"]);
    }

    /// <summary>
    /// A submission whose evaluation has no move at its move number is still logged, named and grouped by the
    /// move number alone, rather than borrowing another evaluation's move or failing.
    /// </summary>
    [Fact]
    public async Task LogXApiAsync_names_a_submission_by_move_number_when_its_evaluation_has_no_such_move()
    {
        var other = await TestScenario.SeedEvaluationAsync(Db, Ct, "Other");
        await Seed(TestData.Move(other.Evaluation.Id, 2, "Other Evaluation's Move"));
        var graph = await TestScenario.SeedEvaluationAsync(Db, Ct);
        var (submission, user) = await SeedUserSubmissionAt(graph, 2);
        CaptureStatement();

        await using var context = NewContext();
        await Service(context, user).LogXApiAsync(new Uri("https://w3id.org/xapi/dod-isd/verbs/submitted"), submission, null, Ct);

        Assert.Equal("User Submission — Move 2", _activity["name"]);
        Assert.Equal("2", _grouping[0]["id"]);
        Assert.Equal("", _grouping[0]["description"]);
    }

    private async Task<(Cite.Api.ViewModels.Submission Submission, UserEntity User)> SeedUserSubmissionAt(
        EvaluationGraph graph, int moveNumber)
    {
        var user = TestData.User();
        await Seed(user, TestData.TeamMembership(graph.Team.Id, user.Id));
        var submission = await TestScenario.SeedSubmissionAsync(Db, Ct, graph, graph.Team.Id, user.Id, moveNumber);
        return (TestMapper.Mapper.Map<Cite.Api.ViewModels.Submission>(submission.Submission), user);
    }

    private async Task<(EvaluationGraph Graph, SubmissionGraph Submission, UserEntity User)> SeedUserSubmission()
    {
        var graph = await TestScenario.SeedEvaluationAsync(Db, Ct);
        var user = TestData.User();
        await Seed(user, TestData.TeamMembership(graph.Team.Id, user.Id));
        var submission = await TestScenario.SeedSubmissionAsync(Db, Ct, graph, graph.Team.Id, user.Id);
        return (graph, submission, user);
    }

    private void CaptureStatement()
    {
        _xApi.IsConfigured().Returns(true);
        _xApi.CreateAsync(Arg.Do<Uri>(x => _verb = x), Arg.Do<Dictionary<string, string>>(x => _activity = x),
            Arg.Any<Dictionary<string, string>>(), Arg.Any<Dictionary<string, string>>(),
            Arg.Do<List<Dictionary<string, string>>>(x => _grouping = x), Arg.Any<Dictionary<string, string>>(), Arg.Any<Guid>(),
            Arg.Any<CancellationToken>()).Returns(true);
    }

    private SubmissionService Service(Cite.Api.Data.CiteContext context, UserEntity user) =>
        new(context, Substitute.For<ICiteAuthorizationService>(),
            new ClaimsPrincipalBuilder().WithUserId(user.Id).Build(), TestMapper.Mapper, new DatabaseOptions(),
            Substitute.For<IMoveService>(), _xApi, NullLogger<SubmissionService>.Instance);
}
