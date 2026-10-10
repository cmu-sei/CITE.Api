# Testing

Cite.Api has an automated test suite in the `Cite.Api.Tests` project. This document says how the suite is built, how to run it, and what is specific to this API. The parts every Crucible API shares are described once, in the Crucible API test standard (`agent-docs/api-testing/` in the workspace: `README.md` for the harness, `CONVENTIONS.md` for how tests are written).

The suite is built on xUnit v3 and NSubstitute and runs against a real PostgreSQL instance started in a container. The files under `Cite.Api.Tests/Support/Shared/` are copied from the standard by its `sync.sh` and are not edited here. Most tests send an HTTP request to the application hosted in process, through the real routes, MVC filters, claims transformer, authorization handlers, AutoMapper profiles and a real database migrated by the real migrations, then assert on the response, on what changed in the database (read back through a fresh context) and on what was broadcast to the SignalR hub. Only collaborators that leave the process are replaced.

Defects the tests found are characterized by ordinary passing tests and described in `agent-docs/api-test-bugs/cite.api.md` in the workspace, never in test comments.

Every authorization call site needs an allowed test whose caller holds exactly the permission (never `Root`) and a near-miss denied test; the standard's `check-repo.js gates`, run by `verify.sh`, fails any gate without them. The few gates the harness cannot meet are listed, each with its reason, in `agent-docs/api-test-gaps/cite.api.txt` in the workspace (today the scoring option reads and the second tier of the scoring category list, whose near miss is answered with a 500 the defect document describes, and the first tier of the scoring category list, which is tested by its other-branch near miss, the ObserveEvaluations caller who gets the masked categories, but which the check cannot credit), never in a file in this repository.

# Running the tests

```bash
dotnet test Cite.Api.Tests
```

Docker must be running. The suite starts and disposes its own PostgreSQL container (`postgres:16-alpine`) through Testcontainers, so there is nothing to install. The container starts when the first test asks for a database, so tests that take none (the requirement handler and mapping tests) still run without Docker.

```bash
dotnet test Cite.Api.Tests --filter "FullyQualifiedName~.EvaluationControllerTests"
dotnet test Cite.Api.Tests -- xUnit.DiagnosticMessages=true   # prints the provider banner
```

# Coverage

```bash
dotnet test Cite.Api.Tests --collect:"XPlat Code Coverage"
```

`coverlet.runsettings` (shared by the standard) is applied by `RunSettingsFilePath` in the test project, with its collector disabled by default, so a plain run collects nothing and a coverage run always excludes `Cite.Api.Migrations.PostgreSQL`, the `Crucible.Common.EntityEvents` content files and generated code. cobertura lists every class twice: read `lines-covered`/`lines-valid` on the root element rather than summing classes. Coverage is a local diagnostic; CI does not collect it.

# Build settings

- `Cite.Api.Tests/Directory.Build.props` (shared) turns warnings into errors for the test project, which makes the xUnit analyzers (xUnit1051 for a missing cancellation token, xUnit2000, xUnit2012, ...) fail the build. NuGet audit and restore codes (NU1901-NU1904, NU1510, NU1701 for TinCan) stay warnings. The repository has no root `Directory.Build.props`, so the application projects are unaffected.
- The root `.editorconfig` raises xUnit1004, so `[Fact(Skip = ...)]` fails the build.
- The repository has no central package management; the test packages carry the versions the standard pins (`agent-docs/api-testing/test-packages.props`) in `Cite.Api.Tests.csproj`, and `sync.sh` checks them.
- The application projects build with compiler warnings today (CS1573 and CS0169 in `Cite.Api`, CS8981 in `Cite.Api.Migrations.PostgreSQL`); `verify.sh` lists them as a note and counts only the test project's own warnings, of which there are none.
- The suite runs in VSTest mode of `dotnet test`, not Microsoft.Testing.Platform; the standard's README says why. Never set `diagnosticMessages` in `xunit.runner.json`: the run hangs after the last test.

# How the harness works

The standard's README describes the shared parts. What cite.api adds:

- **Fixtures** (`AssemblyFixtures.cs`): `DatabaseFixture` over `PostgresTestDatabase<CiteContext>` with the migrations in `Cite.Api.Migrations.PostgreSQL`, and the run-wide `CiteAppFactory`. Every test gets its own database cloned from the migrated template.
- **`CiteContextFactory`** builds test contexts the way `DatabaseExtensions.UseConfiguredDatabase` does: Npgsql with split queries and the entity event interceptor.
- **`CiteAppFactory`** (run-wide, step 1B of the standard's factory template). `Program.Main` runs `InitializeDatabase` with no switch to skip it, so the host is given a throwaway database cloned from the template (`DatabaseFixture.HostDatabase()`), passed as the `Database:Provider` and `ConnectionStrings:PostgreSQL` host settings. `InitializeDatabase` resolves `CiteContext` outside any request, so the factory uses the two-argument `TestDatabaseScope.ReplaceRegistration(services, () => _started ? null : DatabaseFixture.HostDatabase())`: until the host has started such a resolution gets the host database over that session's own services (so the seed's entity events never reach the real handlers or the hub recorder), and afterwards it throws. `CreateHost` builds the one host under a lock, as the template has it, because `WebApplicationFactory.StartServer` takes none. It hosts in Production, so `JsonExceptionFilter` answers a 500 with "A server error occurred." and the exception message as the detail.
- **`TestConfiguration`** overrides the shipped `Database:Provider` (Sqlite) and `Database:DevModeRecreate` (true, which would drop the host database), and turns off claims caching. The identity-provider roles and groups switches stay as shipped: `TestAuthHandler` mints no role or group claims, so they have nothing to read; `UserClaimsServiceTests` drives those paths directly.
- **What is replaced**: token validation (`TestAuthHandler`, with the `cite` scope the global `AuthorizeFilter` requires, registered under the name `Bearer` because `MainHub` names that scheme; Startup's JWT registration is removed first); the context registration; `IHubContext<MainHub>` (`HubRecorder<MainHub>`, read with `Factory.Hub<MainHub>().ToGroup(id)`, or `ToGroups(...)` for a `Clients.Groups(...)` send); `IHttpClientFactory` (`StubHttpClientFactory` over `Factory.OutboundHttp`, which answers the identity provider and Gallery for `GalleryService`); and the hosted `XApiBackgroundService`, removed.
- **`ApiTestBase`** adds `AssertApiError(status, response)`, the shared `AssertJsonError<ApiError>` plus the assertion on `ApiError.Status`: cite.api's `JsonExceptionFilter` answers a thrown exception with its `ApiError` as `application/json`. A characterized 500 asserts the returned `Detail`, the exception message that names the throw. A body or route value MVC cannot bind is answered first by `[ApiController]`'s automatic 400, a ProblemDetails, so `AssertProblem` applies there and `ValidateModelStateFilter` never runs (`Infrastructure/Filters/MalformedRequestTests`).
- **`TestActor`** mirrors `UserClaimsService.GetPermissionClaims`: `WithAllSystemPermissions` (the seeded Administrator role), `WithSystemPermissions(...)` (a minted system role), `OnEvaluation`, `OnScoringModel` and `OnTeam` (a seeded role id or exact permissions; `OnTeam` with neither leaves the role null), `InGroup(groupId)` (a group membership, through which the group's evaluation and scoring model memberships reach the actor), and `OnNewEvaluation`, `OnNewTeam(evaluationId, ...)` and `OnNewScoringModel` for near misses, which mint a resource of their own whose id is on `TestActor.NewEvaluationIds`/`NewTeamIds`/`NewScoringModelIds`. Several services treat any membership on a team of an evaluation as participation, so a near miss that must not participate uses `OnNewTeam` under another evaluation. `TestActorTests` pins every shape.
- **`TestData`** holds the mothers and the seeded role ids (`SystemRoleEntityDefaults`, `EvaluationRoleDefaults`, `ScoringModelRoleEntityDefaults`, `TeamRoleDefaults`; seeded by `HasData`, so they are in the template). **`TestScenario`** (an app extra) seeds the shape most tests start from: an evaluation with its own scoring model copy (one category, one option), move 0 and a team, and `SeedSubmissionAsync` adds a submission tree for it. Seeding goes through the test's own context, whose mediator is a substitute, so it creates no submissions and broadcasts nothing.
- **`TestMapper`** is Startup's AutoMapper configuration, with a private copy of the internal `IgnoreNullSourceValues` convention; `MappingConfigurationTests` guards the copy.
- **`ClaimsPrincipalBuilder`** and **`AuthorizationHarness`** are for the requirement handler tests, the `MainHub` tests on `HubHarness` (which run the real `AuthorizationService` over a principal shaped like the transformer's output) and the service tests' callers. HTTP tests never write claims. The hub's own `[Authorize]` is tested over a real SignalR connection (`MainHubConnectionTests`), with the `Microsoft.AspNetCore.SignalR.Client` package; it uses the WebSocket transport, because over long polling a hub invocation has no request to route `CiteContext` by.
- **Self-tests**: `DatabaseHarnessTests` (isolation probes on the uniquely indexed `system_roles.name`), `HttpHarnessTests` (`api/users` and `api/system-roles`) and `TestActorTests`.

# Adding a test

Follow `CONVENTIONS.md` section 2 of the standard. Put the file where the code under test lives (`Controllers/`, `Hubs/`, `Services/`, `Infrastructure/<Area>/`), derive from `ApiTestBase` for a request, seed with `TestScenario`, `TestData` and `Actor()`, assert on the response and on a `NewContext()` read, and pass `Ct`. Denied cases are near misses (`<Operation>_is_forbidden_for_a_caller_holding_only_<Permission>`, or the right permission `_only_on_another_evaluation`/`_only_on_a_sibling_team`). A behaviour that looks wrong is characterized by a passing test and an entry in `agent-docs/api-test-bugs/cite.api.md`.

# Layout

```
Cite.Api.Tests/
  AssemblyFixtures.cs
  Controllers/        one <Controller>Tests.cs per controller, UnknownIdResponseTests
  Hubs/               MainHubTests (HubHarness), MainHubConnectionTests (a real connection)
  Infrastructure/     Authorization/ (requirement handlers), Extensions/ (ScopePolicyTests: the cite scope), EventHandlers/ (membership broadcasts, on a test-owned HubRecorder),
                      Filters/ (MalformedRequestTests), MappingConfigurationTests
  Services/           services driven directly: UserClaimsService, GalleryService (its own
                      StubHttpMessageHandler), ActionService and DutyService (xAPI, with a test-owned
                      IXApiService substitute)
  Support/            template-derived harness, TestScenario, the three self-tests
  Support/Shared/     copied from agent-docs/api-testing; never edited here
```

# Continuous integration

`.github/workflows/build-and-test.yml` is the standard's workflow with `Cite.Api.Tests` filled in (`sync.sh` keeps it identical). It restores, builds, runs `dotnet test Cite.Api.Tests` with the provider banner on, greps `[Cite.Api.Tests] database provider: PostgreSQL` from the log, and uploads the TRX as `test-results`. There is no `services: postgres:` block; Testcontainers starts the database.
