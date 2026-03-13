// Copyright 2025 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.

using System.Net;
using Cite.Api.Tests.Integration.Fixtures;
using Shouldly;
using Xunit;

namespace Cite.Api.Tests.Integration.Tests.Controllers;

public class HealthCheckTests : IClassFixture<CiteTestContext>
{
    private readonly CiteTestContext _context;

    public HealthCheckTests(CiteTestContext context)
    {
        _context = context;
    }

    [Fact]
    public async Task GetVersion_ReturnsOk()
    {
        // Arrange
        var client = _context.CreateClient();

        // Act
        var response = await client.GetAsync("/api/version");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task GetLiveliness_ReturnsHealthy()
    {
        // Arrange
        var client = _context.CreateClient();

        // Act
        var response = await client.GetAsync("/api/health/live");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.ShouldContain("Healthy");
    }

    [Fact]
    public async Task GetReadiness_ReturnsHealthy()
    {
        // Arrange
        var client = _context.CreateClient();

        // Act
        var response = await client.GetAsync("/api/health/ready");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.ShouldContain("Healthy");
    }
}
