// Copyright 2026 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Cite.Api.Data.Enumerations;
using Cite.Api.Data.Models;
using Cite.Api.Hubs;
using Cite.Api.Tests.Support;
using Cite.Api.ViewModels;
using Action = Cite.Api.ViewModels.Action;

namespace Cite.Api.Tests.Controllers;

/// <summary>
/// An evaluation's <c>HideNamesFromObservers</c>: someone watching a team they are not on (an Observer or a
/// Facilitator of the evaluation, or a holder of the system ObserveEvaluations) gets the team's responses
/// without its members' own submissions and without a user id on the rest. The evaluation's Owner, a holder
/// of the system ViewEvaluations, and the team's own members still get the names. The official submission
/// is the evaluation staff's, so it keeps them.
/// </summary>
public class HideNamesFromObserversTests(DatabaseFixture fixture, CiteAppFactory factory) : ApiTestBase(fixture, factory)
{
    private static readonly Guid Observer = EvaluationRoleDefaults.EvaluationObserverRoleId;
    private static readonly Guid Facilitator = EvaluationRoleDefaults.EvaluationFacilitatorRoleId;
    private static readonly Guid Owner = EvaluationRoleDefaults.EvaluationOwnerRoleId;

    [Theory]
    [InlineData("Observer")]
    [InlineData("Facilitator")]
    public async Task GetByEvaluationTeam_leaves_out_member_submissions_and_authors_for_an_outsider(string role)
    {
        var scene = await SeedScene(hideNames: true);
        var actor = await Actor().OnEvaluation(scene.Graph.Evaluation.Id, role == "Observer" ? Observer : Facilitator).SeedAsync();

        var submissions = await TeamSubmissions(actor, scene);

        AssertMasked(scene, submissions);
    }

    [Fact]
    public async Task GetByEvaluationTeam_masks_a_caller_holding_only_the_system_ObserveEvaluations()
    {
        var scene = await SeedScene(hideNames: true);
        var actor = await Actor().WithSystemPermissions(SystemPermission.ObserveEvaluations).SeedAsync();

        AssertMasked(scene, await TeamSubmissions(actor, scene));
    }

    /// <summary>A member of a sibling team is an outsider to this one.</summary>
    [Fact]
    public async Task GetByEvaluationTeam_masks_an_observer_who_is_on_another_team_of_the_evaluation()
    {
        var scene = await SeedScene(hideNames: true);
        var actor = await Actor().OnEvaluation(scene.Graph.Evaluation.Id, Observer)
            .OnNewTeam(scene.Graph.Evaluation.Id, TeamPermission.ViewTeam).SeedAsync();

        AssertMasked(scene, await TeamSubmissions(actor, scene));
    }

    [Fact]
    public async Task GetByEvaluationTeam_keeps_the_names_for_the_evaluations_owner()
    {
        var scene = await SeedScene(hideNames: true);
        var actor = await Actor().OnEvaluation(scene.Graph.Evaluation.Id, Owner).SeedAsync();

        AssertUnmasked(scene, await TeamSubmissions(actor, scene));
    }

    [Fact]
    public async Task GetByEvaluationTeam_keeps_the_names_for_a_system_administrator()
    {
        var scene = await SeedScene(hideNames: true);

        AssertUnmasked(scene, await ReadAsync<List<Submission>>(await RootClient.GetAsync(TeamSubmissionsUrl(scene), Ct)));
    }

    [Fact]
    public async Task GetByEvaluationTeam_keeps_the_names_for_a_caller_holding_ViewEvaluations_and_ObserveEvaluations()
    {
        var scene = await SeedScene(hideNames: true);
        var actor = await Actor().WithSystemPermissions(SystemPermission.ViewEvaluations, SystemPermission.ObserveEvaluations).SeedAsync();

        AssertUnmasked(scene, await TeamSubmissions(actor, scene));
    }

    /// <summary>An observer who is also on the team sees their teammates' own submissions and names.</summary>
    [Fact]
    public async Task GetByEvaluationTeam_keeps_the_names_for_an_observer_on_the_team_including_teammates()
    {
        var scene = await SeedScene(hideNames: true);
        var actor = await Actor().OnEvaluation(scene.Graph.Evaluation.Id, Observer)
            .OnTeam(scene.Graph.Team.Id, permissions: [TeamPermission.ViewTeam]).SeedAsync();

        AssertUnmasked(scene, await TeamSubmissions(actor, scene));
    }

    /// <summary>A member who is not an observer gets the team's submission with the teammate's comment signed.</summary>
    [Fact]
    public async Task GetByEvaluationTeam_keeps_a_teammates_authorship_on_the_team_submission_for_a_member()
    {
        var scene = await SeedScene(hideNames: true);
        var actor = await Actor().OnTeam(scene.Graph.Team.Id, permissions: [TeamPermission.ViewTeam]).SeedAsync();

        var team = Assert.Single(await TeamSubmissions(actor, scene), x => x.Id == scene.Team.Submission.Id);

        Assert.Equal(scene.Teammate.Id, Comment(team).CreatedBy);
        Assert.Equal(scene.Teammate.Id, Option(team).ModifiedBy);
    }

    [Fact]
    public async Task GetByEvaluationTeam_is_unchanged_for_an_observer_when_the_setting_is_off()
    {
        var scene = await SeedScene(hideNames: false);
        var actor = await Actor().OnEvaluation(scene.Graph.Evaluation.Id, Observer).SeedAsync();

        AssertUnmasked(scene, await TeamSubmissions(actor, scene));
    }

    /// <summary>
    /// The team average is computed from the stored member submissions, so leaving them out of the response
    /// does not change it: its score, category score and selection count are the members'.
    /// </summary>
    [Fact]
    public async Task GetByEvaluationTeam_keeps_the_team_average_of_the_left_out_member_submissions()
    {
        var scene = await SeedScene(hideNames: true, teamAverage: true);
        var observer = await Actor().OnEvaluation(scene.Graph.Evaluation.Id, Observer).SeedAsync();

        var masked = await TeamSubmissions(observer, scene);
        var unmasked = await ReadAsync<List<Submission>>(await RootClient.GetAsync(TeamSubmissionsUrl(scene), Ct));

        var average = Assert.Single(masked, x => x.ScoreIsAnAverage && x.TeamId == scene.Graph.Team.Id);
        var expected = Assert.Single(unmasked, x => x.ScoreIsAnAverage && x.TeamId == scene.Graph.Team.Id);
        Assert.Equal((20.0, 20.0, 2), (average.Score, Category(average).Score, Option(average).SelectedCount));
        Assert.Equal((expected.Score, Category(expected).Score, Option(expected).SelectedCount),
            (average.Score, Category(average).Score, Option(average).SelectedCount));
        // the average is copied from one member's submission, so it loses that member's id and comments
        Assert.Equal((Guid.Empty, (Guid?)null), (average.CreatedBy, Option(average).ModifiedBy));
        Assert.Empty(Option(average).SubmissionComments);
    }

    [Fact]
    public async Task Get_withholds_a_member_submission_from_an_outsider()
    {
        var scene = await SeedScene(hideNames: true);
        var actor = await Actor().OnEvaluation(scene.Graph.Evaluation.Id, Observer).SeedAsync();

        await AssertApiError(HttpStatusCode.Forbidden, await Client(actor).GetAsync($"api/submissions/{scene.Member.Submission.Id}", Ct));
    }

    [Fact]
    public async Task Get_returns_the_team_submission_without_authors_to_an_outsider()
    {
        var scene = await SeedScene(hideNames: true);
        var actor = await Actor().OnEvaluation(scene.Graph.Evaluation.Id, Facilitator).SeedAsync();

        var team = await ReadAsync<Submission>(await Client(actor).GetAsync($"api/submissions/{scene.Team.Submission.Id}", Ct));

        AssertNoNames(team);
        Assert.Equal("Isolated the host", Comment(team).Comment);
    }

    [Fact]
    public async Task Get_returns_the_official_submission_with_its_names_to_an_outsider()
    {
        var scene = await SeedScene(hideNames: true);
        var actor = await Actor().OnEvaluation(scene.Graph.Evaluation.Id, Observer).SeedAsync();

        var official = await ReadAsync<Submission>(await Client(actor).GetAsync($"api/submissions/{scene.Official.Submission.Id}", Ct));

        Assert.Equal(scene.Teammate.Id, Comment(official).CreatedBy);
    }

    [Fact]
    public async Task Get_returns_the_member_submission_to_a_caller_holding_ViewEvaluations()
    {
        var scene = await SeedScene(hideNames: true);
        var actor = await Actor().WithSystemPermissions(SystemPermission.ViewEvaluations).SeedAsync();

        var member = await ReadAsync<Submission>(await Client(actor).GetAsync($"api/submissions/{scene.Member.Submission.Id}", Ct));

        Assert.Equal(scene.Teammate.Id, member.UserId);
    }

    /// <summary>The category, option and comment reads under a submission follow the submission's rule.</summary>
    [Theory]
    [InlineData("submission/{submission}/submissionCategories")]
    [InlineData("submissionCategories/{category}")]
    [InlineData("submissionCategory/{category}/submissionOptions")]
    [InlineData("submissionOptions/{option}")]
    [InlineData("submissionOption/{option}/submissionComments")]
    [InlineData("submissionComments/{comment}")]
    public async Task Submission_parts_follow_the_submissions_rule_for_an_outsider(string route)
    {
        var scene = await SeedScene(hideNames: true);
        var actor = await Actor().OnEvaluation(scene.Graph.Evaluation.Id, Observer).SeedAsync();
        var client = Client(actor);

        await AssertApiError(HttpStatusCode.Forbidden, await client.GetAsync(Url(route, scene.Member, scene.MemberCommentId), Ct));
        var body = await (await client.GetAsync(Url(route, scene.Team, scene.TeamCommentId), Ct)).Content.ReadAsStringAsync(Ct);
        Assert.DoesNotContain(scene.Teammate.Id.ToString(), body);
        Assert.Contains(Guid.Empty.ToString(), body);

        // the Owner still reads the author
        var owner = await Actor().OnEvaluation(scene.Graph.Evaluation.Id, Owner).SeedAsync();
        Assert.Contains(scene.Teammate.Id.ToString(), await (await Client(owner).GetAsync(Url(route, scene.Team, scene.TeamCommentId), Ct)).Content.ReadAsStringAsync(Ct));
    }

    /// <summary>
    /// A team submission's broadcast goes to the team's named group with its names and to its unnamed group
    /// without them, and not to the team's own group, which every watcher of the team is in.
    /// </summary>
    [Fact]
    public async Task Create_broadcasts_the_teams_submission_with_and_without_names_when_the_evaluation_hides_names()
    {
        var graph = await TestScenario.SeedEvaluationAsync(Db, Ct);
        graph.Evaluation.HideNamesFromObservers = true;
        await Db.SaveChangesAsync(Ct);

        var created = await ReadAsync<Submission>(await RootClient.PostAsJsonAsync("api/submissions", NewSubmission(graph), Ct));

        var hub = Factory.Hub<MainHub>();
        Assert.DoesNotContain(hub.ToGroup(graph.Team.Id), x => x.Method == MainHubMethods.SubmissionCreated);
        var named = Assert.IsType<Submission>(Assert.Single(hub.ToGroup(graph.Team.Id + MainHub.NAMES_SHOWN_POSTFIX), x => x.Method == MainHubMethods.SubmissionCreated).Arguments[0]);
        var unnamed = Assert.IsType<Submission>(Assert.Single(hub.ToGroup(graph.Team.Id + MainHub.NAMES_HIDDEN_POSTFIX), x => x.Method == MainHubMethods.SubmissionCreated).Arguments[0]);
        Assert.Equal((created.Id, Root.Id), (named.Id, named.CreatedBy));
        Assert.Equal((created.Id, Guid.Empty), (unnamed.Id, unnamed.CreatedBy));
    }

    /// <summary>A member's submission updates the team average, which goes to the named and unnamed groups too.</summary>
    [Fact]
    public async Task Create_broadcasts_the_team_average_with_and_without_names_when_the_evaluation_hides_names()
    {
        var graph = await TestScenario.SeedEvaluationAsync(Db, Ct);
        graph.Evaluation.HideNamesFromObservers = true;
        await Db.SaveChangesAsync(Ct);
        var teammate = TestData.User(name: "Teammate");
        await Seed(teammate, TestData.TeamMembership(graph.Team.Id, teammate.Id));

        await ReadAsync<Submission>(await RootClient.PostAsJsonAsync("api/submissions", NewSubmission(graph, teammate.Id), Ct));

        var hub = Factory.Hub<MainHub>();
        Assert.DoesNotContain(hub.ToGroup(graph.Team.Id), x => x.Method == MainHubMethods.SubmissionCreated);
        var named = Assert.IsType<Submission>(Assert.Single(hub.ToGroup(graph.Team.Id + MainHub.NAMES_SHOWN_POSTFIX)).Arguments[0]);
        var unnamed = Assert.IsType<Submission>(Assert.Single(hub.ToGroup(graph.Team.Id + MainHub.NAMES_HIDDEN_POSTFIX)).Arguments[0]);
        Assert.Equal((true, true), (named.ScoreIsAnAverage, unnamed.ScoreIsAnAverage));
        Assert.Equal((Root.Id, Guid.Empty), (named.CreatedBy, unnamed.CreatedBy));
        // the member's own submission still goes to that member alone
        Assert.Single(hub.ToGroup(teammate.Id), x => x.Method == MainHubMethods.SubmissionCreated);
    }

    [Fact]
    public async Task Create_broadcasts_the_teams_submission_to_the_team_when_the_evaluation_shows_names()
    {
        var graph = await TestScenario.SeedEvaluationAsync(Db, Ct);

        await ReadAsync<Submission>(await RootClient.PostAsJsonAsync("api/submissions", NewSubmission(graph), Ct));

        var hub = Factory.Hub<MainHub>();
        Assert.Single(hub.ToGroup(graph.Team.Id), x => x.Method == MainHubMethods.SubmissionCreated);
        Assert.Empty(hub.ToGroup(graph.Team.Id + MainHub.NAMES_HIDDEN_POSTFIX));
    }

    [Fact]
    public async Task An_evaluation_shows_names_unless_set()
    {
        var template = TestData.ScoringModel("Template");
        await Seed(template);
        var id = Guid.NewGuid();

        var created = await ReadAsync<Evaluation>(await RootClient.PostAsJsonAsync("api/evaluations", NewEvaluation(id, template.Id, hideNames: null), Ct));

        Assert.False(created.HideNamesFromObservers);
        await using var context = NewContext();
        Assert.False((await context.Evaluations.SingleAsync(x => x.Id == id, Ct)).HideNamesFromObservers);
    }

    [Fact]
    public async Task Create_stores_HideNamesFromObservers()
    {
        var template = TestData.ScoringModel("Template");
        await Seed(template);
        var id = Guid.NewGuid();

        var created = await ReadAsync<Evaluation>(await RootClient.PostAsJsonAsync("api/evaluations", NewEvaluation(id, template.Id, hideNames: true), Ct));

        Assert.True(created.HideNamesFromObservers);
        await using var context = NewContext();
        Assert.True((await context.Evaluations.SingleAsync(x => x.Id == id, Ct)).HideNamesFromObservers);
    }

    /// <summary>The setting can be turned on and off after the evaluation has started (it is Active).</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Update_sets_HideNamesFromObservers_on_an_active_evaluation(bool hideNames)
    {
        var graph = await TestScenario.SeedEvaluationAsync(Db, Ct);
        graph.Evaluation.HideNamesFromObservers = !hideNames;
        await Db.SaveChangesAsync(Ct);
        var actor = await Actor().OnEvaluation(graph.Evaluation.Id, permissions: [EvaluationPermission.EditEvaluation, EvaluationPermission.ViewEvaluation]).SeedAsync();

        var updated = await ReadAsync<Evaluation>(await Client(actor).PutAsJsonAsync($"api/evaluations/{graph.Evaluation.Id}", new
        {
            description = graph.Evaluation.Description,
            status = "Active",
            situationTime = graph.Evaluation.SituationTime,
            hideNamesFromObservers = hideNames
        }, Ct));

        Assert.Equal(hideNames, updated.HideNamesFromObservers);
        var read = await ReadAsync<Evaluation>(await RootClient.GetAsync($"api/evaluations/{graph.Evaluation.Id}", Ct));
        Assert.Equal(hideNames, read.HideNamesFromObservers);
    }

    [Fact]
    public async Task Copy_keeps_HideNamesFromObservers()
    {
        var graph = await TestScenario.SeedEvaluationAsync(Db, Ct, "Source");
        graph.Evaluation.HideNamesFromObservers = true;
        await Db.SaveChangesAsync(Ct);

        var copy = await ReadAsync<Evaluation>(await RootClient.PostAsync($"api/evaluations/{graph.Evaluation.Id}/copy", null, Ct));

        await using var context = NewContext();
        Assert.True((await context.Evaluations.SingleAsync(x => x.Id == copy.Id, Ct)).HideNamesFromObservers);
    }

    [Fact]
    public async Task Json_export_and_import_keep_HideNamesFromObservers()
    {
        var graph = await TestScenario.SeedEvaluationAsync(Db, Ct, "Round Trip");
        graph.Evaluation.HideNamesFromObservers = true;
        await Db.SaveChangesAsync(Ct);
        var exported = await (await RootClient.GetAsync($"api/evaluations/{graph.Evaluation.Id}/json", Ct)).Content.ReadAsByteArrayAsync(Ct);
        var file = new ByteArrayContent(exported);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/json");

        var imported = await ReadAsync<Evaluation>(await RootClient.PostAsync("api/evaluations/json",
            new MultipartFormDataContent { { file, "ToUpload", "evaluation.json" } }, Ct));

        await using var context = NewContext();
        Assert.True((await context.Evaluations.SingleAsync(x => x.Id == imported.Id, Ct)).HideNamesFromObservers);
    }

    /// <summary>Creating an evaluation with the setting sent as null, as a client that has no value for it might.</summary>
    [Fact]
    public async Task Create_treats_a_null_HideNamesFromObservers_as_false()
    {
        var template = TestData.ScoringModel("Template");
        await Seed(template);
        var id = Guid.NewGuid();

        var created = await ReadAsync<Evaluation>(await RootClient.PostAsJsonAsync("api/evaluations", new
        {
            id,
            description = "New Evaluation",
            status = "Active",
            scoringModelId = template.Id,
            situationTime = TestData.DefaultDateCreated,
            hideNamesFromObservers = (bool?)null
        }, Ct));

        Assert.False(created.HideNamesFromObservers);
        Assert.False(await ReadBack(db => db.Evaluations.Where(x => x.Id == id).Select(x => x.HideNamesFromObservers).SingleAsync(Ct)));
    }

    /// <summary>
    /// An update that leaves the setting out, or sends null, keeps it, so a client that does not know about
    /// it (Blueprint activating its evaluation, or an older UI) does not turn it off.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Update_without_HideNamesFromObservers_keeps_it(bool sendNull)
    {
        var graph = await TestScenario.SeedEvaluationAsync(Db, Ct);
        graph.Evaluation.HideNamesFromObservers = true;
        await Db.SaveChangesAsync(Ct);
        object request = sendNull
            ? new { description = "Renamed", status = "Active", situationTime = graph.Evaluation.SituationTime, hideNamesFromObservers = (bool?)null }
            : new { description = "Renamed", status = "Active", situationTime = graph.Evaluation.SituationTime };

        var updated = await ReadAsync<Evaluation>(await RootClient.PutAsJsonAsync($"api/evaluations/{graph.Evaluation.Id}", request, Ct));

        Assert.Equal(("Renamed", (bool?)true), (updated.Description, updated.HideNamesFromObservers));
        Assert.True(await ReadBack(db => db.Evaluations.Where(x => x.Id == graph.Evaluation.Id).Select(x => x.HideNamesFromObservers).SingleAsync(Ct)));
    }

    /// <summary>
    /// An action's ChangedBy and ModifiedBy are the team members who last checked and edited it, so an outsider
    /// of the team gets neither; the Owner and the team's members still do.
    /// </summary>
    [Theory]
    [InlineData("evaluations/{evaluation}/teams/{team}/actions")]
    [InlineData("evaluations/{evaluation}/moves/0/teams/{team}/actions")]
    [InlineData("actions/{action}")]
    public async Task Action_reads_leave_out_who_checked_and_edited_it_for_an_outsider(string route)
    {
        var scene = await SeedActionScene(hideNames: true);
        var observer = await Actor().OnEvaluation(scene.Graph.Evaluation.Id, Observer).SeedAsync();
        var owner = await Actor().OnEvaluation(scene.Graph.Evaluation.Id, Owner).SeedAsync();
        var member = await Actor().OnTeam(scene.Graph.Team.Id, permissions: [TeamPermission.ViewTeam]).SeedAsync();

        var masked = await ActionsBody(observer, route, scene);
        Assert.Contains(scene.Action.Description, masked);
        Assert.DoesNotContain(scene.Teammate.Id.ToString(), masked);
        Assert.Contains(scene.Teammate.Id.ToString(), await ActionsBody(owner, route, scene));
        Assert.Contains(scene.Teammate.Id.ToString(), await ActionsBody(member, route, scene));
    }

    [Fact]
    public async Task Action_reads_are_unchanged_for_an_observer_when_the_setting_is_off()
    {
        var scene = await SeedActionScene(hideNames: false);
        var observer = await Actor().OnEvaluation(scene.Graph.Evaluation.Id, Observer).SeedAsync();

        var action = await ReadAsync<Action>(await Client(observer).GetAsync($"api/actions/{scene.Action.Id}", Ct));

        Assert.Equal((scene.Teammate.Id, scene.Teammate.Id), (action.ChangedBy, action.ModifiedBy));
    }

    /// <summary>
    /// The evaluation-wide reads span every team, so an evaluation Viewer who is on one team gets that team's
    /// actions with their names and the other team's without.
    /// </summary>
    [Theory]
    [InlineData("evaluations/{evaluation}/actions")]
    [InlineData("evaluations/{evaluation}/moves/0/actions")]
    public async Task Evaluation_wide_action_reads_mask_only_the_teams_the_caller_is_not_on(string route)
    {
        var scene = await SeedActionScene(hideNames: true);
        var viewer = await Actor().OnEvaluation(scene.Graph.Evaluation.Id, permissions: [EvaluationPermission.ViewEvaluation])
            .OnNewTeam(scene.Graph.Evaluation.Id, TeamPermission.ViewTeam).SeedAsync();
        var own = TestData.Action(scene.Graph.Evaluation.Id, Assert.Single(viewer.NewTeamIds), description: "Own team");
        await Seed(own);
        own.IsChecked = true;
        own.ChangedBy = viewer.Id;
        own.ModifiedBy = viewer.Id;
        await Db.SaveChangesAsync(Ct);

        var actions = await ReadAsync<List<Action>>(await Client(viewer).GetAsync(ActionsUrl(route, scene), Ct));

        var other = Assert.Single(actions, x => x.Id == scene.Action.Id);
        Assert.Equal(((Guid?)null, (Guid?)null), (other.ChangedBy, other.ModifiedBy));
        var mine = Assert.Single(actions, x => x.Id == own.Id);
        Assert.Equal((viewer.Id, viewer.Id), (mine.ChangedBy, mine.ModifiedBy));
    }

    /// <summary>
    /// An evaluation Editor can check any action without being on the team, and the response leaves out the
    /// team member who last edited it.
    /// </summary>
    [Fact]
    public async Task Check_returns_the_action_without_its_names_to_an_outsider()
    {
        var scene = await SeedActionScene(hideNames: true);
        var editor = await Actor().OnEvaluation(scene.Graph.Evaluation.Id, permissions: [EvaluationPermission.EditEvaluation, EvaluationPermission.ViewEvaluation]).SeedAsync();

        var action = await ReadAsync<Action>(await Client(editor).PutAsync($"api/actions/{scene.Action.Id}/uncheck", null, Ct));

        Assert.Equal((false, (Guid?)null, (Guid?)null), (action.IsChecked, action.ChangedBy, action.ModifiedBy));
    }

    /// <summary>
    /// An action's broadcast goes to the team's named group with its names and to its unnamed group without
    /// them, and not to the team's own group, which every watcher of the team is in.
    /// </summary>
    [Fact]
    public async Task Check_broadcasts_the_action_with_and_without_names_when_the_evaluation_hides_names()
    {
        var scene = await SeedActionScene(hideNames: true);

        await AssertStatus(HttpStatusCode.OK, await RootClient.PutAsync($"api/actions/{scene.Action.Id}/uncheck", null, Ct));

        var hub = Factory.Hub<MainHub>();
        Assert.DoesNotContain(hub.ToGroup(scene.Graph.Team.Id), x => x.Method == MainHubMethods.ActionUpdated);
        var named = Assert.IsType<Action>(Assert.Single(hub.ToGroup(scene.Graph.Team.Id + MainHub.NAMES_SHOWN_POSTFIX), x => x.Method == MainHubMethods.ActionUpdated).Arguments[0]);
        var unnamed = Assert.IsType<Action>(Assert.Single(hub.ToGroup(scene.Graph.Team.Id + MainHub.NAMES_HIDDEN_POSTFIX), x => x.Method == MainHubMethods.ActionUpdated).Arguments[0]);
        Assert.Equal((Root.Id, scene.Teammate.Id), (named.ChangedBy, named.ModifiedBy));
        Assert.Equal(((Guid?)null, (Guid?)null), (unnamed.ChangedBy, unnamed.ModifiedBy));
        // the admin data group, which every test's broadcasts reach, gets the names
        var admin = Assert.Single(hub.ToGroup(MainHub.ADMIN_DATA_GROUP), x => x.Method == MainHubMethods.ActionUpdated && x.Arguments[0] is Action a && a.Id == scene.Action.Id);
        Assert.Equal(Root.Id, ((Action)admin.Arguments[0]).ChangedBy);
    }

    [Fact]
    public async Task Check_broadcasts_the_action_to_the_team_when_the_evaluation_shows_names()
    {
        var scene = await SeedActionScene(hideNames: false);

        await AssertStatus(HttpStatusCode.OK, await RootClient.PutAsync($"api/actions/{scene.Action.Id}/uncheck", null, Ct));

        var hub = Factory.Hub<MainHub>();
        Assert.Single(hub.ToGroup(scene.Graph.Team.Id), x => x.Method == MainHubMethods.ActionUpdated);
        Assert.Empty(hub.ToGroup(scene.Graph.Team.Id + MainHub.NAMES_HIDDEN_POSTFIX));
    }

    private sealed record ActionScene(EvaluationGraph Graph, UserEntity Teammate, ActionEntity Action);

    /// <summary>A team action that its member, the teammate, checked and last edited.</summary>
    private async Task<ActionScene> SeedActionScene(bool hideNames)
    {
        var graph = await TestScenario.SeedEvaluationAsync(Db, Ct);
        graph.Evaluation.HideNamesFromObservers = hideNames;
        await Db.SaveChangesAsync(Ct);
        var teammate = TestData.User(name: "Teammate");
        var action = TestData.Action(graph.Evaluation.Id, graph.Team.Id);
        await Seed(teammate, TestData.TeamMembership(graph.Team.Id, teammate.Id), action);
        // an added row's ModifiedBy is cleared by CiteContext, so the check and the edit are an update
        action.IsChecked = true;
        action.ChangedBy = teammate.Id;
        action.ModifiedBy = teammate.Id;
        await Db.SaveChangesAsync(Ct);

        return new ActionScene(graph, teammate, action);
    }

    private async Task<string> ActionsBody(TestActor actor, string route, ActionScene scene)
    {
        var response = await Client(actor).GetAsync(ActionsUrl(route, scene), Ct);
        await AssertStatus(HttpStatusCode.OK, response);

        return await response.Content.ReadAsStringAsync(Ct);
    }

    private static string ActionsUrl(string route, ActionScene scene) =>
        "api/" + route
            .Replace("{evaluation}", scene.Graph.Evaluation.Id.ToString())
            .Replace("{team}", scene.Graph.Team.Id.ToString())
            .Replace("{action}", scene.Action.Id.ToString());

    private sealed record Scene(
        EvaluationGraph Graph,
        UserEntity Teammate,
        SubmissionGraph Team,
        SubmissionGraph Member,
        SubmissionGraph Official,
        Guid TeamCommentId,
        Guid MemberCommentId);

    /// <summary>
    /// A team whose member, the teammate, selected and commented on the team's submission, the official one,
    /// and their own; with <paramref name="teamAverage"/> a second member's submission too, and the scoring
    /// model showing the team average, so the average of two member scores (10 and 30) is 20.
    /// </summary>
    private async Task<Scene> SeedScene(bool hideNames, bool teamAverage = false)
    {
        var graph = await TestScenario.SeedEvaluationAsync(Db, Ct);
        graph.Evaluation.HideNamesFromObservers = hideNames;
        graph.ScoringModel.UseOfficialScore = true;
        graph.ScoringModel.UseTeamAverageScore = teamAverage;
        await Db.SaveChangesAsync(Ct);
        var teammate = TestData.User(name: "Teammate");
        await Seed(teammate, TestData.TeamMembership(graph.Team.Id, teammate.Id));

        var team = await TestScenario.SeedSubmissionAsync(Db, Ct, graph, graph.Team.Id);
        var member = await TestScenario.SeedSubmissionAsync(Db, Ct, graph, graph.Team.Id, teammate.Id);
        var official = await TestScenario.SeedSubmissionAsync(Db, Ct, graph);
        var teamComment = await SignedBy(teammate, team, "Isolated the host", score: 0);
        var memberComment = await SignedBy(teammate, member, "My own note", score: 10);
        await SignedBy(teammate, official, "Official note", score: 0);
        if (teamAverage)
        {
            var second = TestData.User(name: "Second");
            await Seed(second, TestData.TeamMembership(graph.Team.Id, second.Id));
            await SignedBy(second, await TestScenario.SeedSubmissionAsync(Db, Ct, graph, graph.Team.Id, second.Id), "Second note", score: 30);
        }

        return new Scene(graph, teammate, team, member, official, teamComment, memberComment);
    }

    /// <summary>Has <paramref name="user"/> select the submission's option, comment on it and score it.</summary>
    private async Task<Guid> SignedBy(UserEntity user, SubmissionGraph submission, string text, double score)
    {
        var comment = TestData.SubmissionComment(submission.Option.Id, text);
        comment.CreatedBy = user.Id;
        await Seed(comment);
        // an added row's ModifiedBy is cleared by CiteContext, so the selection and scores are an update
        submission.Option.IsSelected = true;
        submission.Option.ModifiedBy = user.Id;
        submission.Category.Score = score;
        submission.Category.ModifiedBy = user.Id;
        submission.Submission.Score = score;
        submission.Submission.ModifiedBy = user.Id;
        await Db.SaveChangesAsync(Ct);

        return comment.Id;
    }

    private async Task<List<Submission>> TeamSubmissions(TestActor actor, Scene scene) =>
        await ReadAsync<List<Submission>>(await Client(actor).GetAsync(TeamSubmissionsUrl(scene), Ct));

    private static string TeamSubmissionsUrl(Scene scene) =>
        $"api/evaluations/{scene.Graph.Evaluation.Id}/teams/{scene.Graph.Team.Id}/submissions";

    private static void AssertMasked(Scene scene, List<Submission> submissions)
    {
        Assert.DoesNotContain(submissions, x => x.UserId != null);
        Assert.DoesNotContain(submissions, x => x.Id == scene.Member.Submission.Id);
        var team = Assert.Single(submissions, x => x.Id == scene.Team.Submission.Id);
        AssertNoNames(team);
        Assert.Equal("Isolated the host", Comment(team).Comment);
        // the official submission is the staff's, so it keeps its names
        var official = Assert.Single(submissions, x => x.Id == scene.Official.Submission.Id);
        Assert.Equal(scene.Teammate.Id, Comment(official).CreatedBy);
    }

    private static void AssertUnmasked(Scene scene, List<Submission> submissions)
    {
        Assert.Equal(scene.Teammate.Id, Assert.Single(submissions, x => x.Id == scene.Member.Submission.Id).UserId);
        var team = Assert.Single(submissions, x => x.Id == scene.Team.Submission.Id);
        Assert.Equal(scene.Teammate.Id, Comment(team).CreatedBy);
        Assert.Equal(scene.Teammate.Id, Option(team).ModifiedBy);
        Assert.Equal(scene.Teammate.Id, team.ModifiedBy);
    }

    private static void AssertNoNames(Submission submission)
    {
        Assert.Null(submission.UserId);
        Assert.Equal((Guid.Empty, (Guid?)null), (submission.CreatedBy, submission.ModifiedBy));
        Assert.Equal((Guid.Empty, (Guid?)null), (Category(submission).CreatedBy, Category(submission).ModifiedBy));
        Assert.Equal((Guid.Empty, (Guid?)null), (Option(submission).CreatedBy, Option(submission).ModifiedBy));
        Assert.Equal((Guid.Empty, (Guid?)null), (Comment(submission).CreatedBy, Comment(submission).ModifiedBy));
    }

    private static SubmissionCategory Category(Submission submission) => Assert.Single(submission.SubmissionCategories);

    private static SubmissionOption Option(Submission submission) => Assert.Single(Category(submission).SubmissionOptions);

    private static SubmissionComment Comment(Submission submission) => Assert.Single(Option(submission).SubmissionComments);

    private static string Url(string route, SubmissionGraph submission, Guid commentId) =>
        "api/" + route
            .Replace("{submission}", submission.Submission.Id.ToString())
            .Replace("{category}", submission.Category.Id.ToString())
            .Replace("{option}", submission.Option.Id.ToString())
            .Replace("{comment}", commentId.ToString());

    private static object NewSubmission(EvaluationGraph graph, Guid? userId = null) => new
    {
        id = Guid.Empty,
        score = 0,
        status = "Active",
        scoringModelId = graph.ScoringModel.Id,
        userId,
        evaluationId = graph.Evaluation.Id,
        teamId = graph.Team.Id,
        moveNumber = 1
    };

    private static object NewEvaluation(Guid id, Guid scoringModelId, bool? hideNames) => hideNames is null
        ? new { id, description = "New Evaluation", status = "Active", scoringModelId, situationTime = TestData.DefaultDateCreated }
        : new { id, description = "New Evaluation", status = "Active", scoringModelId, situationTime = TestData.DefaultDateCreated, hideNamesFromObservers = hideNames.Value };
}
