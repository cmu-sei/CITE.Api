// Copyright 2026 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.

using System.Net;
using System.Net.Http.Json;
using Cite.Api.Data;
using Cite.Api.Tests.Support;
using Microsoft.EntityFrameworkCore;

namespace Cite.Api.Tests.Controllers;

public class EnumRangeTests(DatabaseFixture fixture, CiteAppFactory factory) : ApiTestBase(fixture, factory)
{
    /// <summary>An integer that belongs to none of the API's enum types.</summary>
    private const int Undefined = 999;

    [Theory]
    [InlineData("evaluation-status", "POST")]
    [InlineData("evaluation-status", "PUT")]
    [InlineData("category-selection", "POST")]
    [InlineData("category-selection", "PUT")]
    [InlineData("model-status", "POST")]
    [InlineData("model-status", "PUT")]
    [InlineData("model-display", "POST")]
    [InlineData("model-display", "PUT")]
    [InlineData("submission-status", "PUT")]
    public async Task Write_stores_and_returns_an_undefined_enum_value(string field, string method)
    {
        var arranged = await Body(field, method);
        using var request = new HttpRequestMessage(new HttpMethod(method), arranged.Route) { Content = JsonContent.Create(arranged.Body) };

        var response = await RootClient.SendAsync(request, Ct);

        await AssertStatus(arranged.Status, response);
        var body = await ReadAsync<System.Text.Json.JsonElement>(response);
        Assert.Equal(Undefined, body.GetProperty(arranged.Property).GetInt32());
        Assert.Equal(Undefined, await ReadBack(db => Stored(db, field, arranged.Id)));
    }

    private async Task<(Guid Id, string Route, object Body, string Property, HttpStatusCode Status)> Body(string field, string method)
    {
        var graph = await TestScenario.SeedEvaluationAsync(Db, Ct);
        var create = method == "POST";
        var status = create ? HttpStatusCode.Created : HttpStatusCode.OK;
        var id = create ? Guid.NewGuid() : field == "evaluation-status" ? graph.Evaluation.Id : field == "category-selection" ? graph.Category.Id : graph.ScoringModel.Id;
        switch (field)
        {
            case "evaluation-status":
                return (id, create ? "api/evaluations" : $"api/evaluations/{id}", new { id, description = "Undefined Status", status = Undefined, scoringModelId = graph.ScoringModel.Id, situationTime = TestData.DefaultDateCreated }, "status", status);
            case "category-selection":
                return (id, create ? "api/scoringCategories" : $"api/scoringCategories/{id}", new { id, description = "Undefined Selection", scoringModelId = graph.ScoringModel.Id, scoringOptionSelection = Undefined }, "scoringOptionSelection", status);
            case "model-status":
                return (id, create ? "api/scoringModels" : $"api/scoringModels/{id}", new { id, description = "Undefined Status", status = Undefined, calculationEquation = "{sum}" }, "status", status);
            case "model-display":
                return (id, create ? "api/scoringModels" : $"api/scoringModels/{id}", new { id, description = "Undefined Display", status = "Active", rightSideDisplay = Undefined, calculationEquation = "{sum}" }, "rightSideDisplay", status);
            case "submission-status":
                var submission = await TestScenario.SeedSubmissionAsync(Db, Ct, graph, graph.Team.Id, Root.Id);
                return (submission.Submission.Id, $"api/submissions/{submission.Submission.Id}", new { id = submission.Submission.Id, evaluationId = graph.Evaluation.Id, scoringModelId = graph.ScoringModel.Id, teamId = graph.Team.Id, userId = Root.Id, status = Undefined }, "status", status);
            default:
                throw new ArgumentOutOfRangeException(nameof(field));
        }
    }

    private async Task<int> Stored(CiteContext db, string field, Guid id) => field switch
    {
        "evaluation-status" => (int)await db.Evaluations.Where(x => x.Id == id).Select(x => x.Status).SingleAsync(Ct),
        "category-selection" => (int)await db.ScoringCategories.Where(x => x.Id == id).Select(x => x.ScoringOptionSelection).SingleAsync(Ct),
        "model-status" => (int)await db.ScoringModels.Where(x => x.Id == id).Select(x => x.Status).SingleAsync(Ct),
        "model-display" => (int)await db.ScoringModels.Where(x => x.Id == id).Select(x => x.RightSideDisplay).SingleAsync(Ct),
        "submission-status" => (int)await db.Submissions.Where(x => x.Id == id).Select(x => x.Status).SingleAsync(Ct),
        _ => throw new ArgumentOutOfRangeException(nameof(field))
    };
}
