// Copyright 2026 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.

﻿using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cite.Api.Migrations.PostgreSQL.Migrations
{
    /// <inheritdoc />
    public partial class AddViewPastOfficialScoreToSeededTeamRoles : Migration
    {
        // ViewPastOfficialScore (4) lets every seeded team role keep reading past moves' official scores now that
        // the scoresheet checks the permission. Append it rather than overwrite the arrays, so permissions an
        // administrator added to a seeded role are kept.
        private const string SeededTeamRoleIds =
            "'a2cc11c1-9fd1-402b-9937-0f6ede1066c3', '1cfce79f-f344-4cb1-b33a-55de8dc1ccb3', " +
            "'c442cf49-e26e-45c5-be7c-00e710d2e055', 'b52ef031-65ee-4597-b768-b73480e6de67'";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($@"
                UPDATE team_roles
                SET permissions = array_append(permissions, 4)
                WHERE id IN ({SeededTeamRoleIds})
                  AND NOT (4 = ANY(permissions));");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($@"
                UPDATE team_roles
                SET permissions = array_remove(permissions, 4)
                WHERE id IN ({SeededTeamRoleIds});");
        }
    }
}
