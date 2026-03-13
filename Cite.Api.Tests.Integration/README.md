# Cite.Api.Tests.Integration

Integration tests for the CITE API using Testcontainers and a real PostgreSQL database.

## Purpose

Tests the full API stack including controllers, services, Entity Framework, and database interactions. Uses WebApplicationFactory to host the API in-memory and Testcontainers to provide a real PostgreSQL instance for realistic data persistence testing.

## Files

- `Fixtures/CiteTestContext.cs` - WebApplicationFactory setup with PostgreSQL Testcontainer
- `Tests/Controllers/HealthCheckTests.cs` - Health and readiness endpoint tests
- `Tests/Controllers/UserControllerTests.cs` - User API endpoint integration tests

## Key Patterns

### Test Context Setup
`CiteTestContext` extends `WebApplicationFactory<Program>` and implements `IAsyncLifetime`:

1. **Container Initialization** - Starts a PostgreSQL Testcontainer before tests
2. **Service Replacement** - Swaps authentication and authorization services with test implementations
3. **Database Creation** - Runs EF Core migrations via `EnsureCreatedAsync()`
4. **Cleanup** - Disposes container after all tests complete

### Test Structure
```csharp
public class UserControllerTests : IClassFixture<CiteTestContext>
{
    private readonly CiteTestContext _context;

    public UserControllerTests(CiteTestContext context)
    {
        _context = context;
    }

    [Fact]
    public async Task CreateUser_ReturnsCreated()
    {
        // Arrange
        var client = _context.CreateClient();
        var user = new User { Id = Guid.NewGuid(), Name = "Test User" };

        // Act
        var response = await client.PostAsJsonAsync("/api/users", user);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }
}
```

### Database Seeding
Tests can directly manipulate the database via `GetDbContext()`:

```csharp
using (var dbContext = _context.GetDbContext())
{
    dbContext.Users.Add(new UserEntity { Id = userId, Name = "Seeded User" });
    await dbContext.SaveChangesAsync();
}
```

## Test Infrastructure

### CiteTestContext Configuration
- **Environment**: "Test"
- **Database**: PostgreSQL Testcontainer (auto-removed after tests)
- **Authentication**: `TestAuthenticationHandler` - bypasses OIDC
- **Authorization**: `TestAuthorizationService` - allows all operations
- **CITE Authorization**: `TestCiteAuthorizationService` - allows all permission checks

### Testcontainer Setup
```csharp
_container = new PostgreSqlBuilder()
    .WithHostname("localhost")
    .WithUsername("foundry")
    .WithPassword("foundry")
    .WithImage("postgres:latest")
    .WithAutoRemove(true)
    .WithCleanUp(true)
    .Build();
```

## Test Categories

### Health Checks
- `/api/version` - Version endpoint
- `/api/health/live` - Liveness probe
- `/api/health/ready` - Readiness probe

### API Endpoints
- **User Controller** - GET, POST, DELETE user operations
- Full HTTP lifecycle testing (request -> routing -> controller -> service -> database -> response)

## Running Tests

```bash
# From cite.api/ directory
dotnet test Cite.Api.Tests.Integration

# Run specific test class
dotnet test Cite.Api.Tests.Integration --filter "FullyQualifiedName~UserControllerTests"

# Run with verbose output
dotnet test Cite.Api.Tests.Integration --logger "console;verbosity=detailed"

# Requires Docker running for Testcontainers
```

## Prerequisites

- **Docker** - Must be running for PostgreSQL Testcontainers
- **Docker daemon** - Accessible from the test environment

## Dependencies

- **xUnit** 2.9.3 - Test framework
- **Testcontainers.PostgreSql** 4.0.0 - PostgreSQL test containers
- **Microsoft.AspNetCore.Mvc.Testing** 10.0.1 - WebApplicationFactory
- **Npgsql.EntityFrameworkCore.PostgreSQL** 10.0.0 - PostgreSQL provider
- **FakeItEasy** 8.3.0 - Mocking framework
- **Shouldly** 4.2.1 - Fluent assertions
- **AutoFixture** 4.18.1 - Test data generation
- **Crucible.Common.Testing** - Shared test utilities (TestAuthenticationHandler, TestClaimsTransformation)

## Troubleshooting

### Container Fails to Start
- Ensure Docker is running
- Check Docker daemon accessibility
- Verify port 5432 is not in use

### Database Migration Errors
- Check EF Core migrations are up-to-date
- Verify connection string configuration
- Ensure PostgreSQL version compatibility

## Code Coverage

Minimum target: 80% coverage of controller endpoints and full-stack integration paths.

## Copyright

Copyright 2026 Carnegie Mellon University. All Rights Reserved.
Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.
