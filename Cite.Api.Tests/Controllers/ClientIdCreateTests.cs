// Copyright 2026 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.

using System.Net;
using System.Net.Http.Json;
using Cite.Api.Data;
using Cite.Api.Tests.Support;
using Microsoft.EntityFrameworkCore;

namespace Cite.Api.Tests.Controllers;

public class ClientIdCreateTests(DatabaseFixture fixture, CiteAppFactory factory) : ApiTestBase(fixture, factory)
{
    [Theory]
    [InlineData("actions")]
    [InlineData("duties")]
    [InlineData("evaluations")]
    [InlineData("evaluations/memberships")]
    [InlineData("groups")]
    [InlineData("groups/memberships")]
    [InlineData("moves")]
    [InlineData("scoringCategories")]
    [InlineData("scoringModels")]
    [InlineData("scoringModels/memberships")]
    [InlineData("submissionCategories")]
    [InlineData("submissionComments")]
    [InlineData("submissionOptions")]
    [InlineData("submissions/comments")]
    [InlineData("system-roles")]
    [InlineData("teams")]
    [InlineData("teams/memberships")]
    [InlineData("teamTypes")]
    public async Task Create_answers_an_existing_client_id_with_a_server_error_and_leaves_the_row_unchanged(string resource)
    {
        var arranged = await Duplicate(resource);
        var before = await ReadBack(db => Stored(db, resource, arranged.Id));

        var response = await RootClient.PostAsJsonAsync(arranged.Route, arranged.Body, Ct);

        var error = await AssertApiError(HttpStatusCode.InternalServerError, response);
        Assert.Equal("An error occurred while saving the entity changes. See the inner exception for details.", error.Detail);
        Assert.Equal(before, await ReadBack(db => Stored(db, resource, arranged.Id)));
    }

    private async Task<(Guid Id, string Route, object Body)> Duplicate(string resource)
    {
        var graph = await TestScenario.SeedEvaluationAsync(Db, Ct);
        var submission = await TestScenario.SeedSubmissionAsync(Db, Ct, graph, graph.Team.Id, Root.Id);
        var user = TestData.User();
        await Seed(user);
        switch (resource)
        {
            case "actions":
                var action = TestData.Action(graph.Evaluation.Id, graph.Team.Id);
                await Seed(action);
                return (action.Id, "api/actions", new { id = action.Id, evaluationId = graph.Evaluation.Id, teamId = graph.Team.Id, description = "Replacement" });
            case "duties":
                var duty = TestData.Duty(graph.Evaluation.Id, graph.Team.Id);
                await Seed(duty);
                return (duty.Id, "api/duties", new { id = duty.Id, evaluationId = graph.Evaluation.Id, teamId = graph.Team.Id, name = "Replacement" });
            case "evaluations":
                return (graph.Evaluation.Id, "api/evaluations", new { id = graph.Evaluation.Id, scoringModelId = graph.ScoringModel.Id, description = "Replacement", status = "Active", situationTime = TestData.DefaultDateCreated });
            case "evaluations/memberships":
                var evaluationMembership = TestData.EvaluationMembership(graph.Evaluation.Id, user.Id, TestData.EvaluationRoles.Member);
                await Seed(evaluationMembership);
                return (evaluationMembership.Id, $"api/evaluations/{graph.Evaluation.Id}/memberships", new { id = evaluationMembership.Id, evaluationId = graph.Evaluation.Id, userId = user.Id, roleId = TestData.EvaluationRoles.Owner });
            case "groups":
                var group = TestData.Group();
                await Seed(group);
                return (group.Id, "api/groups", new { id = group.Id, name = "Replacement" });
            case "groups/memberships":
                var membershipGroup = TestData.Group();
                var groupMembership = TestData.GroupMembership(membershipGroup.Id, user.Id);
                var otherUser = TestData.User(name: "Replacement User");
                await Seed(membershipGroup, groupMembership, otherUser);
                return (groupMembership.Id, $"api/groups/{membershipGroup.Id}/memberships", new { id = groupMembership.Id, groupId = membershipGroup.Id, userId = otherUser.Id });
            case "moves":
                return (graph.Move.Id, "api/moves", new { id = graph.Move.Id, evaluationId = graph.Evaluation.Id, moveNumber = 17, description = "Replacement", situationTime = TestData.DefaultDateCreated });
            case "scoringCategories":
                return (graph.Category.Id, "api/scoringCategories", new { id = graph.Category.Id, scoringModelId = graph.ScoringModel.Id, description = "Replacement" });
            case "scoringModels":
                return (graph.ScoringModel.Id, "api/scoringModels", new { id = graph.ScoringModel.Id, description = "Replacement", status = "Active", calculationEquation = "{sum}" });
            case "scoringModels/memberships":
                var modelMembership = TestData.ScoringModelMembership(graph.ScoringModel.Id, user.Id, TestData.ScoringModelRoles.Editor);
                await Seed(modelMembership);
                return (modelMembership.Id, $"api/scoringModels/{graph.ScoringModel.Id}/memberships", new { id = modelMembership.Id, scoringModelId = graph.ScoringModel.Id, userId = user.Id, roleId = TestData.ScoringModelRoles.Owner });
            case "submissionCategories":
                return (submission.Category.Id, "api/submissionCategories", new { id = submission.Category.Id, submissionId = submission.Submission.Id, scoringCategoryId = graph.Category.Id, score = 41 });
            case "submissionComments":
            case "submissions/comments":
                var comment = TestData.SubmissionComment(submission.Option.Id);
                await Seed(comment);
                var route = resource == "submissionComments" ? "api/submissionComments" : $"api/submissions/{submission.Submission.Id}/comments";
                return (comment.Id, route, new { id = comment.Id, submissionOptionId = submission.Option.Id, comment = "Replacement" });
            case "submissionOptions":
                return (submission.Option.Id, "api/submissionOptions", new { id = submission.Option.Id, submissionCategoryId = submission.Category.Id, scoringOptionId = graph.Option.Id, isSelected = true });
            case "system-roles":
                var role = TestData.SystemRole("Original Role");
                await Seed(role);
                return (role.Id, "api/system-roles", new { id = role.Id, name = "Replacement", permissions = Array.Empty<string>() });
            case "teams":
                return (graph.Team.Id, "api/teams", new { id = graph.Team.Id, evaluationId = graph.Evaluation.Id, teamTypeId = graph.TeamType.Id, name = "Replacement", shortName = "R" });
            case "teams/memberships":
                var teamMembership = TestData.TeamMembership(graph.Team.Id, user.Id, TestData.TeamRoles.Member);
                var replacementUser = TestData.User(name: "Replacement User");
                await Seed(teamMembership, replacementUser);
                return (teamMembership.Id, $"api/teams/{graph.Team.Id}/memberships", new { id = teamMembership.Id, teamId = graph.Team.Id, userId = replacementUser.Id, roleId = TestData.TeamRoles.Owner });
            case "teamTypes":
                return (graph.TeamType.Id, "api/teamTypes", new { id = graph.TeamType.Id, name = "Replacement" });
            default:
                throw new ArgumentOutOfRangeException(nameof(resource));
        }
    }

    private async Task<string> Stored(CiteContext db, string resource, Guid id) => resource switch
    {
        "actions" => await db.Actions.Where(x => x.Id == id).Select(x => x.Description).SingleAsync(Ct),
        "duties" => await db.Duties.Where(x => x.Id == id).Select(x => x.Name).SingleAsync(Ct),
        "evaluations" => await db.Evaluations.Where(x => x.Id == id).Select(x => x.Description).SingleAsync(Ct),
        "evaluations/memberships" => (await db.EvaluationMemberships.Where(x => x.Id == id).Select(x => x.RoleId).SingleAsync(Ct)).ToString(),
        "groups" => await db.Groups.Where(x => x.Id == id).Select(x => x.Name).SingleAsync(Ct),
        "groups/memberships" => (await db.GroupMemberships.Where(x => x.Id == id).Select(x => x.UserId).SingleAsync(Ct)).ToString(),
        "moves" => await db.Moves.Where(x => x.Id == id).Select(x => x.Description).SingleAsync(Ct),
        "scoringCategories" => await db.ScoringCategories.Where(x => x.Id == id).Select(x => x.Description).SingleAsync(Ct),
        "scoringModels" => await db.ScoringModels.Where(x => x.Id == id).Select(x => x.Description).SingleAsync(Ct),
        "scoringModels/memberships" => (await db.ScoringModelMemberships.Where(x => x.Id == id).Select(x => x.RoleId).SingleAsync(Ct)).ToString(),
        "submissionCategories" => (await db.SubmissionCategories.Where(x => x.Id == id).Select(x => x.Score).SingleAsync(Ct)).ToString(),
        "submissionComments" or "submissions/comments" => await db.SubmissionComments.Where(x => x.Id == id).Select(x => x.Comment).SingleAsync(Ct),
        "submissionOptions" => (await db.SubmissionOptions.Where(x => x.Id == id).Select(x => x.IsSelected).SingleAsync(Ct)).ToString(),
        "system-roles" => await db.SystemRoles.Where(x => x.Id == id).Select(x => x.Name).SingleAsync(Ct),
        "teams" => await db.Teams.Where(x => x.Id == id).Select(x => x.Name).SingleAsync(Ct),
        "teams/memberships" => (await db.TeamMemberships.Where(x => x.Id == id).Select(x => x.UserId).SingleAsync(Ct)).ToString(),
        "teamTypes" => await db.TeamTypes.Where(x => x.Id == id).Select(x => x.Name).SingleAsync(Ct),
        _ => throw new ArgumentOutOfRangeException(nameof(resource))
    };
}
