# cite.api

This project provides a restful api for the Collaborative Incident Threat Evaluator.

By default, cite.api is available at localhost:4720, with the swagger page at localhost:4720/api/index.html.

# ATTENTION: PosgreSQL version 15 or greater is required to run CITE API!!!

# Database Migrations

When the data model is changed, a new database migration must be created.  From the Cite.Api directory, run this command to create the new migration:
    dotnet ef migrations add <new_migration_name> --project ../Cite.Api.Migrations.PostgreSQL/Cite.Api.Migrations.PostgreSQL.csproj
Running the app will automatically migrate the database.
To Roll back a migration, first update the database to the previous migration
    dotnet ef database update <previous_migration_name> --project ../Cite.Api.Migrations.PostgreSQL/Cite.Api.Migrations.PostgreSQL.csproj
Then remove the migration
    dotnet ef migrations remove --project ../Cite.Api.Migrations.PostgreSQL/Cite.Api.Migrations.PostgreSQL.csproj

# Permissions

SystemAdmin permission required for:
    * User admin
    * Evaluation create/delete

Content Developer permission required for:
    * ScoringModel create/update/delete
    * Evaluation update

CanIncrementMove required for:
    * Evaluation update

CanSubmit permission required for:
    * Submission update for a team

CanModify permission required for:
    * Setting SubmissionOption value for a team

Authenticated user has permission to:
    * Create a Submission
