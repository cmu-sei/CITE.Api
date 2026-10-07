// Copyright 2026 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.

using System.Net;
using Cite.Api.Tests.Support;

namespace Cite.Api.Tests.Controllers;

/// <summary>
/// The health endpoints (<c>HealthCheckController</c> and the <c>MapHealthChecks</c> routes in
/// <c>Startup.Configure</c>) and the version, all open to anonymous callers.
/// </summary>
public class HealthCheckControllerTests(DatabaseFixture fixture, CiteAppFactory factory) : ApiTestBase(fixture, factory)
{
    [Fact]
    public async Task Live_answers_healthy_without_an_identity()
    {
        var response = await Client().GetAsync("api/health/live", Ct);

        await AssertStatus(HttpStatusCode.OK, response);
        Assert.Contains("Healthy", await response.Content.ReadAsStringAsync(Ct));
    }

    /// <summary>The readiness check reaches the host's PostgreSQL database through AddNpgSql.</summary>
    [Fact]
    public async Task Ready_answers_healthy_without_an_identity()
    {
        var response = await Client().GetAsync("api/health/ready", Ct);

        await AssertStatus(HttpStatusCode.OK, response);
        Assert.Contains("Healthy", await response.Content.ReadAsStringAsync(Ct));
    }

    [Fact]
    public async Task Version_answers_the_informational_version_as_text_without_an_identity()
    {
        var response = await Client().GetAsync("api/version", Ct);

        await AssertStatus(HttpStatusCode.OK, response);
        Assert.StartsWith("0.0.0", await response.Content.ReadAsStringAsync(Ct));
    }
}
