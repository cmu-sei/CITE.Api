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
using Shouldly;
using Xunit;

namespace Cite.Api.Tests.Unit.Services;

[Trait("Category", "Unit")]
public class UserServiceTests
{
    private readonly IMapper _fakeMapper;
    private readonly ClaimsPrincipal _fakeUser;
    private readonly IAuthorizationService _fakeAuthorizationService;
    private readonly IUserClaimsService _fakeUserClaimsService;
    private readonly ILogger<IUserService> _fakeLogger;

    private static readonly Guid TestUserId = Guid.Parse("00000000-0000-0000-0000-000000000099");

    public UserServiceTests()
    {
        _fakeMapper = A.Fake<IMapper>();
        _fakeUser = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim("sub", TestUserId.ToString()),
            new Claim(ClaimTypes.NameIdentifier, TestUserId.ToString())
        }, "Test"));
        _fakeAuthorizationService = A.Fake<IAuthorizationService>();
        _fakeUserClaimsService = A.Fake<IUserClaimsService>();
        _fakeLogger = A.Fake<ILogger<IUserService>>();
    }

    [Fact]
    public async Task GetAsync_WhenUsersExist_ReturnsAllUsers()
    {
        // Arrange
        using var context = TestDbContextFactory.Create<CiteContext>();
        var entities = new List<UserEntity>
        {
            new() { Id = Guid.NewGuid(), Name = "User A" },
            new() { Id = Guid.NewGuid(), Name = "User B" }
        };
        context.Users.AddRange(entities);
        context.SaveChanges();

        var expected = new List<User>
        {
            new() { Id = entities[0].Id, Name = "User A" },
            new() { Id = entities[1].Id, Name = "User B" }
        };
        A.CallTo(() => _fakeMapper.Map<IEnumerable<User>>(A<IEnumerable<UserEntity>>.Ignored))
            .Returns(expected);

        var sut = new UserService(
            context,
            _fakeUser as IPrincipal,
            _fakeAuthorizationService,
            _fakeUserClaimsService,
            _fakeLogger,
            _fakeMapper);

        // Act
        var result = await sut.GetAsync(CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.Count().ShouldBe(2);
    }

    [Fact]
    public async Task GetAsync_WithId_WhenUserExists_ReturnsUser()
    {
        // Arrange
        using var context = TestDbContextFactory.Create<CiteContext>();
        var userId = Guid.NewGuid();
        var entity = new UserEntity { Id = userId, Name = "Test User" };
        context.Users.Add(entity);
        context.SaveChanges();

        var expected = new User { Id = userId, Name = "Test User" };
        A.CallTo(() => _fakeMapper.Map<User>(A<UserEntity>.Ignored)).Returns(expected);

        var sut = new UserService(
            context,
            _fakeUser as IPrincipal,
            _fakeAuthorizationService,
            _fakeUserClaimsService,
            _fakeLogger,
            _fakeMapper);

        // Act
        var result = await sut.GetAsync(userId, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.Id.ShouldBe(userId);
        result.Name.ShouldBe("Test User");
    }

    [Fact]
    public async Task GetAsync_WithId_WhenUserNotFound_ReturnsNull()
    {
        // Arrange
        using var context = TestDbContextFactory.Create<CiteContext>();
        var userId = Guid.NewGuid();
        A.CallTo(() => _fakeMapper.Map<User>(A<UserEntity>.That.IsNull())).Returns(null!);

        var sut = new UserService(
            context,
            _fakeUser as IPrincipal,
            _fakeAuthorizationService,
            _fakeUserClaimsService,
            _fakeLogger,
            _fakeMapper);

        // Act
        var result = await sut.GetAsync(userId, CancellationToken.None);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task DeleteAsync_WhenDeletingSelf_ThrowsForbiddenException()
    {
        // Arrange - attempt to delete own user
        using var context = TestDbContextFactory.Create<CiteContext>();
        var entity = new UserEntity { Id = TestUserId, Name = "Self" };
        context.Users.Add(entity);
        context.SaveChanges();

        var sut = new UserService(
            context,
            _fakeUser as IPrincipal,
            _fakeAuthorizationService,
            _fakeUserClaimsService,
            _fakeLogger,
            _fakeMapper);

        // Act & Assert
        await Should.ThrowAsync<Exception>(async () =>
            await sut.DeleteAsync(TestUserId, CancellationToken.None));
    }

    [Fact]
    public async Task DeleteAsync_WhenUserExists_ReturnsTrue()
    {
        // Arrange
        using var context = TestDbContextFactory.Create<CiteContext>();
        var userId = Guid.NewGuid();
        var entity = new UserEntity { Id = userId, Name = "Other User" };
        context.Users.Add(entity);
        context.SaveChanges();

        var sut = new UserService(
            context,
            _fakeUser as IPrincipal,
            _fakeAuthorizationService,
            _fakeUserClaimsService,
            _fakeLogger,
            _fakeMapper);

        // Act
        var result = await sut.DeleteAsync(userId, CancellationToken.None);

        // Assert
        result.ShouldBeTrue();
        context.Users.Any(u => u.Id == userId).ShouldBeFalse();
    }
}
