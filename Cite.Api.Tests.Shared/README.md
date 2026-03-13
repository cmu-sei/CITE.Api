# Cite.Api.Tests.Shared

Shared test fixtures and utilities for the CITE API test suite.

## Purpose

Provides common test infrastructure, including AutoFixture customizations for CITE domain entities. Used by both unit and integration test projects to ensure consistent test data generation.

## Files

- `Fixtures/CiteCustomization.cs` - AutoFixture customization that registers specimen builders for all CITE entities

## Key Entities

The `CiteCustomization` class configures AutoFixture to generate valid test data for:

- `EvaluationEntity` - Top-level evaluation container
- `ScoringModelEntity` - Scoring configuration and equations
- `ScoringCategoryEntity` - Category within a scoring model
- `ScoringOptionEntity` - Selectable scoring options
- `SubmissionEntity` - User/team score submissions
- `SubmissionCategoryEntity` - Category-level submission data
- `TeamEntity` - Teams within an evaluation
- `TeamTypeEntity` - Team classification (e.g., Red Team, Blue Team)
- `UserEntity` - System users
- `ActionEntity` - Actions taken during a move
- `MoveEntity` - Time-based evaluation phases
- `DutyEntity` - User duty assignments
- `GroupEntity` - User groups for permissions

All entities inherit from `BaseEntity` (Guid Id, DateCreated, DateModified, CreatedBy, ModifiedBy).

## Usage

Register the customization with AutoFixture:

```csharp
var fixture = new Fixture();
fixture.Customize(new CiteCustomization());

var evaluation = fixture.Create<EvaluationEntity>();
var team = fixture.Create<TeamEntity>();
```

## Dependencies

- **AutoFixture** 4.18.1 - Test data generation
- **AutoFixture.AutoFakeItEasy** 4.18.1 - Mocking integration
- **AutoFixture.Xunit2** 4.18.1 - xUnit integration
- **Crucible.Common.Testing** - Shared test utilities (GuidIdBuilder, DateTimeOffsetBuilder)

## Running Tests

This is a shared library. Run tests from dependent test projects:

```bash
# From cite.api/ directory
dotnet test Cite.Api.Tests.Unit
dotnet test Cite.Api.Tests.Integration
```

## Copyright

Copyright 2026 Carnegie Mellon University. All Rights Reserved.
Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.
