// Copyright 2025 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.

using System.Net;
using Cite.Api.Tests.Integration.Fixtures;
using TUnit.Core;

namespace Cite.Api.Tests.Integration.Tests.Controllers;

[Category("Integration")]
[ClassDataSource<CiteTestContext>(Shared = SharedType.PerTestSession)]
public class HealthCheckTests(CiteTestContext context)
{
    [Test]
    public async Task GetVersion_WhenCalled_ReturnsOk()
    {
        // Arrange
        var client = context.CreateClient();

        // Act
        var response = await client.GetAsync("/api/version");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        await Assert.That(content).IsNotEmpty();
    }

    [Test]
    public async Task GetLiveliness_WhenHealthy_ReturnsHealthy()
    {
        // Arrange
        var client = context.CreateClient();

        // Act
        var response = await client.GetAsync("/api/health/live");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        await Assert.That(content).Contains("Healthy");
    }

    [Test]
    public async Task GetReadiness_WhenHealthy_ReturnsHealthy()
    {
        // Arrange
        var client = context.CreateClient();

        // Act
        var response = await client.GetAsync("/api/health/ready");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        await Assert.That(content).Contains("Healthy");
    }
}
