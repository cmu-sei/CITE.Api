# Cite.Api.Tests.Unit

Unit tests for the CITE API business logic and service layer.

## Purpose

Tests individual services, AutoMapper profiles, and core business logic in isolation using in-memory databases and mocked dependencies. No external services or real databases required.

## Files

- `MappingConfigurationTests.cs` - Validates AutoMapper profile configuration
- `Services/EvaluationServiceTests.cs` - EvaluationService CRUD operations and status transitions
- `Services/TeamServiceTests.cs` - TeamService CRUD operations
- `Services/UserServiceTests.cs` - UserService CRUD operations

## Key Patterns

### Mocking Strategy
- **FakeItEasy** for creating test doubles (mocks, stubs)
- **TestDbContextFactory.Create<CiteContext>()** for in-memory Entity Framework Core database
- **Shouldly** for fluent assertions
- **xUnit** test framework with `[Fact]` attributes

### Test Structure
```csharp
public class EvaluationServiceTests
{
    private readonly IMapper _fakeMapper;
    private readonly ClaimsPrincipal _fakeUser;
    // ... other fakes

    public EvaluationServiceTests()
    {
        _fakeMapper = A.Fake<IMapper>();
        _fakeUser = new ClaimsPrincipal(...);
        // ... initialize all dependencies
    }

    [Fact]
    public async Task GetAsync_WithId_WhenEvaluationNotFound_ReturnsNull()
    {
        // Arrange
        using var context = TestDbContextFactory.Create<CiteContext>();
        var sut = new EvaluationService(context, ...);

        // Act
        var result = await sut.GetAsync(evaluationId, CancellationToken.None);

        // Assert
        result.ShouldBeNull();
    }
}
```

### Service Under Test (SUT) Pattern
Each test constructs the service directly with mocked dependencies, allowing precise control over behavior and verification of interactions.

## Test Categories

### AutoMapper Tests
- Configuration validation
- Mapper creation verification
- Custom value resolver validation for nullable types

### Service Tests
- **CRUD Operations** - Create, Read, Update, Delete
- **Entity Relationships** - Foreign key and navigation property handling
- **Status Transitions** - Evaluation workflow state changes
- **Authorization** - Permission-based access control
- **Error Handling** - Exception scenarios (not found, invalid state)

## Running Tests

```bash
# From cite.api/ directory
dotnet test Cite.Api.Tests.Unit

# Run specific test class
dotnet test Cite.Api.Tests.Unit --filter "FullyQualifiedName~EvaluationServiceTests"

# Run with coverage
dotnet test Cite.Api.Tests.Unit --collect:"XPlat Code Coverage"

# Run in verbose mode
dotnet test Cite.Api.Tests.Unit --logger "console;verbosity=detailed"
```

## Dependencies

- **xUnit** 2.9.3 - Test framework
- **FakeItEasy** 8.3.0 - Mocking framework
- **Shouldly** 4.2.1 - Fluent assertions
- **AutoFixture** 4.18.1 - Test data generation
- **Microsoft.EntityFrameworkCore.InMemory** 10.0.1 - In-memory database for testing
- **MockQueryable.FakeItEasy** 7.0.3 - LINQ query mocking
- **Crucible.Common.Testing** - Shared test utilities (TestDbContextFactory)

## Code Coverage

Minimum target: 80% across all service and mapping classes.

## Copyright

Copyright 2026 Carnegie Mellon University. All Rights Reserved.
Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.
