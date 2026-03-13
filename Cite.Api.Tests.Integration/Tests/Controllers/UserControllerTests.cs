// Copyright 2025 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.

using System.Net;
using System.Net.Http.Json;
using Cite.Api.Data.Models;
using Cite.Api.Tests.Integration.Fixtures;
using Cite.Api.ViewModels;
using Crucible.Common.Testing.Auth;
using TUnit.Core;

namespace Cite.Api.Tests.Integration.Tests.Controllers;

[Category("Integration")]
[ClassDataSource<CiteTestContext>(Shared = SharedType.PerTestSession)]
public class UserControllerTests(CiteTestContext context)
{
    [Test]
    public async Task GetUsers_WhenCalled_ReturnsOk()
    {
        // Arrange
        var client = context.CreateClient();

        // Act
        var response = await client.GetAsync("/api/users");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task CreateUser_WithValidUser_ReturnsCreated()
    {
        // Arrange
        var client = context.CreateClient();
        var userId = Guid.NewGuid();
        var user = new User
        {
            Id = userId,
            Name = "Test User"
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/users", user);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Created);
        var created = await response.Content.ReadFromJsonAsync<User>();
        await Assert.That(created).IsNotNull();
        await Assert.That(created.Name).IsEqualTo("Test User");
    }

    [Test]
    public async Task GetUser_WhenExists_ReturnsOk()
    {
        // Arrange
        var client = context.CreateClient();

        // Seed a user in the database
        var userId = Guid.NewGuid();
        using (var dbContext = context.GetDbContext())
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
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var user = await response.Content.ReadFromJsonAsync<User>();
        await Assert.That(user).IsNotNull();
        await Assert.That(user.Name).IsEqualTo("Seeded User");
    }

    [Test]
    public async Task DeleteUser_WhenExists_ReturnsNoContent()
    {
        // Arrange
        var client = context.CreateClient();

        // Seed a user in the database
        var userId = Guid.NewGuid();
        using (var dbContext = context.GetDbContext())
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
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NoContent);
    }
}
