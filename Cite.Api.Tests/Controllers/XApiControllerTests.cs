// Copyright 2026 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.

using System.Net;
using Microsoft.EntityFrameworkCore;
using Cite.Api.Tests.Support;

namespace Cite.Api.Tests.Controllers;

/// <summary>
/// <c>XApiController</c>: xAPI "viewed" and "observed" statements, sent only when <c>XApiOptions</c> is
/// enabled, which appsettings.json leaves off. No endpoint checks a permission.
/// </summary>
public class XApiControllerTests(DatabaseFixture fixture, CiteAppFactory factory) : ApiTestBase(fixture, factory)
{
    /// <summary>A request with no identity is answered with a 401; the anonymous client is the case under test.</summary>
    [Fact]
    public async Task ViewedEvaluationDashboard_without_an_identity_is_unauthorized()
    {
        await AssertStatus(HttpStatusCode.Unauthorized, await Client().GetAsync($"api/xapi/viewed/evaluation/{Guid.NewGuid()}/dashboard", Ct));
    }

    /// <summary>No permission is checked, and with xAPI off nothing is queued.</summary>
    [Fact]
    public async Task ViewedEvaluationDashboard_answers_ok_and_queues_nothing_while_xapi_is_off()
    {
        var graph = await TestScenario.SeedEvaluationAsync(Db, Ct);
        var actor = await Actor().SeedAsync();

        await AssertStatus(HttpStatusCode.OK, await Client(actor).GetAsync($"api/xapi/viewed/evaluation/{graph.Evaluation.Id}/dashboard", Ct));

        await using var context = NewContext();
        Assert.False(await context.XApiQueuedStatements.AnyAsync(Ct));
    }

    /// <summary>No permission is checked, and with xAPI off nothing is queued.</summary>
    [Fact]
    public async Task ViewedEvaluationScoresheet_answers_ok_and_queues_nothing_while_xapi_is_off()
    {
        var graph = await TestScenario.SeedEvaluationAsync(Db, Ct);
        var actor = await Actor().SeedAsync();

        await AssertStatus(HttpStatusCode.OK, await Client(actor).GetAsync($"api/xapi/viewed/evaluation/{graph.Evaluation.Id}/scoresheet", Ct));

        await using var context = NewContext();
        Assert.False(await context.XApiQueuedStatements.AnyAsync(Ct));
    }

    /// <summary>With xAPI off, an observed statement is answered with a 500.</summary>
    [Fact]
    public async Task ObservedEvaluationDashboard_answers_with_a_server_error_while_xapi_is_off()
    {
        var graph = await TestScenario.SeedEvaluationAsync(Db, Ct);

        var error = await AssertApiError(HttpStatusCode.InternalServerError,
            await RootClient.GetAsync($"api/xapi/observed/evaluation/{graph.Evaluation.Id}/team/{graph.Team.Id}/dashboard", Ct));
        Assert.Equal("Exception of type 'System.Exception' was thrown.", error.Detail);
    }

    // Same case as ObservedEvaluationDashboard_answers_with_a_server_error_while_xapi_is_off.
    [Fact]
    public async Task ObservedEvaluationScoresheet_answers_with_a_server_error_while_xapi_is_off()
    {
        var graph = await TestScenario.SeedEvaluationAsync(Db, Ct);

        var error = await AssertApiError(HttpStatusCode.InternalServerError,
            await RootClient.GetAsync($"api/xapi/observed/evaluation/{graph.Evaluation.Id}/team/{graph.Team.Id}/scoresheet", Ct));
        Assert.Equal("Exception of type 'System.Exception' was thrown.", error.Detail);
    }
}
