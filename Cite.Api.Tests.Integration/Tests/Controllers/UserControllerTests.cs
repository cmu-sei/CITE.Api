// Copyright 2025 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.

using System.Net;
using System.Net.Http.Json;
using Cite.Api.Data.Models;
using Cite.Api.Tests.Integration.Fixtures;
using Cite.Api.ViewModels;
using Crucible.Common.Testing.Auth;
using Shouldly;
using Xunit;

namespace Cite.Api.Tests.Integration.Tests.Controllers;

public class UserControllerTests : IClassFixture<CiteTestContext>
{
    private readonly CiteTestContext _context;

    public UserControllerTests(CiteTestContext context)
    {
        _context = context;
    }

    [Fact]
    public async Task GetUsers_ReturnsOk()
    {
        // Arrange
        var client = _context.CreateClient();

        // Act
        var response = await client.GetAsync("/api/users");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task CreateUser_ReturnsCreated()
    {
        // Arrange
        var client = _context.CreateClient();
        var userId = Guid.NewGuid();
        var user = new User
        {
            Id = userId,
            Name = "Test User"
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/users", user);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var created = await response.Content.ReadFromJsonAsync<User>();
        created.ShouldNotBeNull();
        created.Name.ShouldBe("Test User");
    }

    [Fact]
    public async Task GetUser_WhenExists_ReturnsOk()
    {
        // Arrange
        var client = _context.CreateClient();

        // Seed a user in the database
        var userId = Guid.NewGuid();
        using (var dbContext = _context.GetDbContext())
        {
            dbContext.Users.Add(new UserEntity
            {
                Id = userId,
                Name = "Seeded User",
                CreatedBy = TestAuthenticationUser.DefaultUserId
            });
            await dbContext.SaveChangesAsync();
        }

        // Act
        var response = await client.GetAsync($"/api/users/{userId}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var user = await response.Content.ReadFromJsonAsync<User>();
        user.ShouldNotBeNull();
        user.Name.ShouldBe("Seeded User");
    }

    [Fact]
    public async Task DeleteUser_WhenExists_ReturnsNoContent()
    {
        // Arrange
        var client = _context.CreateClient();

        // Seed a user in the database
        var userId = Guid.NewGuid();
        using (var dbContext = _context.GetDbContext())
        {
            dbContext.Users.Add(new UserEntity
            {
                Id = userId,
                Name = "User To Delete",
                CreatedBy = TestAuthenticationUser.DefaultUserId
            });
            await dbContext.SaveChangesAsync();
        }

        // Act
        var response = await client.DeleteAsync($"/api/users/{userId}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }
}
