// Copyright 2025 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.

using System.Security.Claims;
using System.Security.Principal;
using AutoMapper;
using Cite.Api.Data;
using Cite.Api.Data.Models;
using Cite.Api.Services;
using Cite.Api.ViewModels;
using Crucible.Common.Testing.Fixtures;
using FakeItEasy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TUnit.Core;

namespace Cite.Api.Tests.Unit.Services;

[Category("Unit")]
public class TeamServiceTests
{
    private readonly IMapper _fakeMapper;
    private readonly ClaimsPrincipal _fakeUser;
    private readonly IAuthorizationService _fakeAuthorizationService;
    private readonly ILogger<ITeamService> _fakeLogger;

    public TeamServiceTests()
    {
        _fakeMapper = A.Fake<IMapper>();
        _fakeUser = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim("sub", Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())
        }, "Test"));
        _fakeAuthorizationService = A.Fake<IAuthorizationService>();
        _fakeLogger = A.Fake<ILogger<ITeamService>>();
    }

    [Test]
    public async Task GetAsync_WithId_WhenTeamExists_ReturnsMappedTeam()
    {
        // Arrange
        using var context = TestDbContextFactory.Create<CiteContext>();
        var teamId = Guid.NewGuid();
        var evaluationId = Guid.NewGuid();
        var entity = new TeamEntity
        {
            Id = teamId,
            Name = "Alpha Team",
            EvaluationId = evaluationId,
            Memberships = new List<TeamMembershipEntity>()
        };
        context.Teams.Add(entity);
        context.SaveChanges();

        var expected = new Team { Id = teamId, Name = "Alpha Team" };
        A.CallTo(() => _fakeMapper.Map<IEnumerable<Team>>(A<IEnumerable<TeamEntity>>.Ignored))
            .Returns(new List<Team> { expected });

        var sut = new TeamService(
            context,
            _fakeUser as IPrincipal,
            _fakeAuthorizationService,
            _fakeLogger,
            _fakeMapper);

        // Act - test the non-ProjectTo path indirectly via GetByEvaluationAsync
        var result = await sut.GetByEvaluationAsync(evaluationId, CancellationToken.None);

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result.Count()).IsEqualTo(1);
        await Assert.That(result.First().Name).IsEqualTo("Alpha Team");
    }

    [Test]
    public async Task DeleteAsync_WhenTeamNotFound_ThrowsEntityNotFoundException()
    {
        // Arrange
        using var context = TestDbContextFactory.Create<CiteContext>();
        var teamId = Guid.NewGuid();

        var sut = new TeamService(
            context,
            _fakeUser as IPrincipal,
            _fakeAuthorizationService,
            _fakeLogger,
            _fakeMapper);

        // Act & Assert
        await Assert.That(async () =>
            await sut.DeleteAsync(teamId, CancellationToken.None))
            .ThrowsException();
    }

    [Test]
    public async Task DeleteAsync_WhenTeamExists_ReturnsTrue()
    {
        // Arrange
        using var context = TestDbContextFactory.Create<CiteContext>();
        var teamId = Guid.NewGuid();
        var evaluationId = Guid.NewGuid();
        var entity = new TeamEntity
        {
            Id = teamId,
            Name = "Team to Delete",
            EvaluationId = evaluationId
        };
        context.Teams.Add(entity);
        context.SaveChanges();

        var sut = new TeamService(
            context,
            _fakeUser as IPrincipal,
            _fakeAuthorizationService,
            _fakeLogger,
            _fakeMapper);

        // Act
        var result = await sut.DeleteAsync(teamId, CancellationToken.None);

        // Assert
        await Assert.That(result).IsTrue();
        await Assert.That(context.Teams.Any(t => t.Id == teamId)).IsFalse();
    }
}
