// Copyright 2026 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.

using System.Net;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Cite.Api.Tests.Support;

namespace Cite.Api.Tests.Infrastructure.Filters;

/// <summary>
/// Requests MVC cannot bind. <c>BaseController</c> carries <c>[ApiController]</c>, so the framework's
/// automatic model-state response answers before the application's <c>ValidateModelStateFilter</c> runs.
/// </summary>
public class MalformedRequestTests(DatabaseFixture fixture, CiteAppFactory factory) : ApiTestBase(fixture, factory)
{
    /// <summary>A request with no identity is answered with a 401 before the body is read; the anonymous client is the case under test.</summary>
    [Fact]
    public async Task A_malformed_body_without_an_identity_is_unauthorized()
    {
        await AssertStatus(HttpStatusCode.Unauthorized, await Client().PostAsync("api/groups", Malformed(), Ct));
    }

    [Fact]
    public async Task A_malformed_body_is_answered_with_a_problem_details_400_and_nothing_is_stored()
    {
        var response = await RootClient.PostAsync("api/groups", Malformed(), Ct);

        var problem = await AssertProblem(HttpStatusCode.BadRequest, response);
        Assert.NotEmpty(problem.Extensions);
        await using var context = NewContext();
        Assert.False(await context.Groups.AnyAsync(Ct));
    }

    [Fact]
    public async Task A_route_id_that_is_not_a_guid_is_answered_with_a_problem_details_400()
    {
        await AssertProblem(HttpStatusCode.BadRequest, await RootClient.PutAsync("api/groups/not-a-guid", new StringContent("{}", Encoding.UTF8, "application/json"), Ct));
    }

    private static StringContent Malformed() => new("{\"name\": ", Encoding.UTF8, "application/json");
}
