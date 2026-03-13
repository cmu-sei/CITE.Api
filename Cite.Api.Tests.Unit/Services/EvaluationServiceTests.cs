// Copyright 2025 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.

using System.Security.Claims;
using System.Security.Principal;
using AutoMapper;
using Cite.Api.Data;
using Cite.Api.Data.Enumerations;
using Cite.Api.Data.Models;
using Cite.Api.Services;
using Cite.Api.ViewModels;
using Crucible.Common.Testing.Fixtures;
using FakeItEasy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Shouldly;
using Xunit;

namespace Cite.Api.Tests.Unit.Services;

public class EvaluationServiceTests
{
    private readonly IMapper _fakeMapper;
    private readonly ClaimsPrincipal _fakeUser;
    private readonly ISubmissionService _fakeSubmissionService;
    private readonly IMoveService _fakeMoveService;
    private readonly IScoringModelService _fakeScoringModelService;
    private readonly ITeamTypeService _fakeTeamTypeService;
    private readonly IUserClaimsService _fakeUserClaimsService;
    private readonly ILogger<EvaluationService> _fakeLogger;

    public EvaluationServiceTests()
    {
        _fakeMapper = A.Fake<IMapper>();
        _fakeUser = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim("sub", Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())
        }, "Test"));
        _fakeSubmissionService = A.Fake<ISubmissionService>();
        _fakeMoveService = A.Fake<IMoveService>();
        _fakeScoringModelService = A.Fake<IScoringModelService>();
        _fakeTeamTypeService = A.Fake<ITeamTypeService>();
        _fakeUserClaimsService = A.Fake<IUserClaimsService>();
        _fakeLogger = A.Fake<ILogger<EvaluationService>>();
    }

    [Fact]
    public async Task GetAsync_WithId_WhenEvaluationNotFound_ReturnsNull()
    {
        // Arrange
        using var context = TestDbContextFactory.Create<CiteContext>();
        var evaluationId = Guid.NewGuid();
        A.CallTo(() => _fakeMapper.Map<ViewModels.Evaluation>(A<EvaluationEntity>.That.IsNull()))
            .Returns(null!);

        var sut = new EvaluationService(
            context,
            _fakeUser as IPrincipal,
            _fakeMapper,
            _fakeSubmissionService,
            _fakeMoveService,
            _fakeScoringModelService,
            _fakeTeamTypeService,
            _fakeUserClaimsService,
            _fakeLogger);

        // Act
        var result = await sut.GetAsync(evaluationId, CancellationToken.None);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetAsync_WithId_WhenEvaluationExists_ReturnsMappedEvaluation()
    {
        // Arrange
        using var context = TestDbContextFactory.Create<CiteContext>();
        var evaluationId = Guid.NewGuid();
        var entity = new EvaluationEntity
        {
            Id = evaluationId,
            Description = "Test Eval",
            Status = ItemStatus.Active,
            SituationTime = DateTime.UtcNow,
            Teams = new HashSet<TeamEntity>(),
            Moves = new HashSet<MoveEntity>()
        };
        context.Evaluations.Add(entity);
        context.SaveChanges();

        var expected = new ViewModels.Evaluation { Id = evaluationId, Description = "Test Eval" };
        A.CallTo(() => _fakeMapper.Map<ViewModels.Evaluation>(A<EvaluationEntity>.Ignored))
            .Returns(expected);

        var sut = new EvaluationService(
            context,
            _fakeUser as IPrincipal,
            _fakeMapper,
            _fakeSubmissionService,
            _fakeMoveService,
            _fakeScoringModelService,
            _fakeTeamTypeService,
            _fakeUserClaimsService,
            _fakeLogger);

        // Act
        var result = await sut.GetAsync(evaluationId, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.Id.ShouldBe(evaluationId);
        result.Description.ShouldBe("Test Eval");
    }

    [Fact]
    public async Task DeleteAsync_WhenEvaluationExists_ReturnsTrue()
    {
        // Arrange
        using var context = TestDbContextFactory.Create<CiteContext>();
        var evaluationId = Guid.NewGuid();
        var entity = new EvaluationEntity
        {
            Id = evaluationId,
            Description = "To Delete",
            SituationTime = DateTime.UtcNow
        };
        context.Evaluations.Add(entity);
        context.SaveChanges();

        var sut = new EvaluationService(
            context,
            _fakeUser as IPrincipal,
            _fakeMapper,
            _fakeSubmissionService,
            _fakeMoveService,
            _fakeScoringModelService,
            _fakeTeamTypeService,
            _fakeUserClaimsService,
            _fakeLogger);

        // Act
        var result = await sut.DeleteAsync(evaluationId, CancellationToken.None);

        // Assert
        result.ShouldBeTrue();
        context.Evaluations.Any(e => e.Id == evaluationId).ShouldBeFalse();
    }

    [Fact]
    public async Task DeleteAsync_WhenEvaluationNotFound_ThrowsEntityNotFoundException()
    {
        // Arrange
        using var context = TestDbContextFactory.Create<CiteContext>();
        var evaluationId = Guid.NewGuid();

        var sut = new EvaluationService(
            context,
            _fakeUser as IPrincipal,
            _fakeMapper,
            _fakeSubmissionService,
            _fakeMoveService,
            _fakeScoringModelService,
            _fakeTeamTypeService,
            _fakeUserClaimsService,
            _fakeLogger);

        // Act & Assert
        await Should.ThrowAsync<Exception>(async () =>
            await sut.DeleteAsync(evaluationId, CancellationToken.None));
    }
}
