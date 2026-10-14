# cite.api

This project provides a restful api for the Collaborative Incident Threat Evaluator.

By default, cite.api is available at localhost:4720, with the swagger page at localhost:4720/api/index.html.

The API targets .NET 10 (the SDK version is pinned in `global.json`). From the Cite.Api directory, `dotnet run` starts it with the `Cite.Api` launch profile. The `Dockerfile` in the repository root builds a `prod` image and a `debug` image, both listening on port 8080.

# ATTENTION: PostgreSQL version 15 or greater is required to run CITE API!!!

# Database

The default `appsettings.json` sets `Database:Provider` to `Sqlite` and `Database:DevModeRecreate` to `true`. With these values the database is deleted and recreated on every start, and no migrations are run (migrations only exist for PostgreSQL). For a persistent database, set `Database:Provider` to `PostgreSQL`, set `ConnectionStrings:PostgreSQL`, and set `Database:DevModeRecreate` to `false`. As environment variables these are `Database__Provider`, `ConnectionStrings__PostgreSQL` and `Database__DevModeRecreate`.

# Database Migrations

When the data model is changed, a new database migration must be created. The migrations assembly is chosen from `Database:Provider`, so `Database:Provider` must be `PostgreSQL` when running these commands (for example by setting the `Database__Provider=PostgreSQL` environment variable), and the `dotnet-ef` tool must be installed (`dotnet tool install --global dotnet-ef`). From the Cite.Api directory, run this command to create the new migration:
    dotnet ef migrations add <new_migration_name> --project ../Cite.Api.Migrations.PostgreSQL/Cite.Api.Migrations.PostgreSQL.csproj
When `Database:Provider` is `PostgreSQL`, running the app will automatically migrate the database.
To Roll back a migration, first update the database to the previous migration
    dotnet ef database update <previous_migration_name> --project ../Cite.Api.Migrations.PostgreSQL/Cite.Api.Migrations.PostgreSQL.csproj
Then remove the migration
    dotnet ef migrations remove --project ../Cite.Api.Migrations.PostgreSQL/Cite.Api.Migrations.PostgreSQL.csproj

# Permissions

Access is governed by permissions that are granted through roles. See [Cite.Api/docs/Permissions.md](Cite.Api/docs/Permissions.md) for details.

System permissions apply across all of CITE and are granted by a user's System Role:
    * CreateScoringModels, ViewScoringModels, EditScoringModels, ManageScoringModels
    * CreateEvaluations, ViewEvaluations, EditEvaluations, ManageEvaluations, ExecuteEvaluations, ObserveEvaluations
    * ViewUsers, ManageUsers, ViewRoles, ManageRoles, ViewGroups, ManageGroups, ViewTeamTypes, ManageTeamTypes

The default System Roles are Administrator (all permissions), Content Developer and Observer.

Resource permissions apply to a single evaluation, scoring model or team and are granted by a membership role on that resource. Evaluation and scoring model memberships can be held by a user or by a group; team memberships are held by users:
    * Evaluation: ViewEvaluation, EditEvaluation, ManageEvaluation, ExecuteEvaluation, ObserveEvaluation, ParticipateInEvaluation
    * Scoring model: ViewScoringModel, EditScoringModel, ManageScoringModel
    * Team: ViewTeam, EditTeamScore, SubmitTeamScore, ManageTeam, ViewPastOfficialScore, ViewCurrentOfficialScore, EditOfficialScore

Roles can also be taken from the identity provider's token (`ClaimsTransformation` settings in `appsettings.json`).

# Testing

The tests are in the `Cite.Api.Tests` project. Docker must be running, because the suite starts its own PostgreSQL container through Testcontainers. From the repository root, run:
    dotnet test Cite.Api.Tests
See [docs/Testing.md](docs/Testing.md) for how the suite is built and how to add a test.
