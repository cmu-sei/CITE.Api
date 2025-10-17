/*
 Copyright 2025 Carnegie Mellon University. All Rights Reserved. 
 Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.
*/

﻿using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cite.Api.Migrations.PostgreSQL.Migrations
{
    /// <inheritdoc />
    public partial class keepexistingpermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "scoring_model_roles",
                keyColumn: "id",
                keyValue: new Guid("39aa296e-05ba-4fb0-8d74-c92cf3354c60"),
                column: "name",
                value: "Viewer");

            migrationBuilder.UpdateData(
                table: "system_roles",
                keyColumn: "id",
                keyValue: new Guid("1da3027e-725d-4753-9455-a836ed9bdb1e"),
                columns: new[] { "description", "name" },
                values: new object[] { "Can View all Evaluations and Scoring Models, but cannot make any changes.", "Viewer" });

            migrationBuilder.UpdateData(
                table: "system_roles",
                keyColumn: "id",
                keyValue: new Guid("d80b73c3-95d7-4468-8650-c62bbd082507"),
                column: "description",
                value: "Can create and manage their own Evaluations and Scoring Models.");

            // assign content developers
            migrationBuilder.Sql(@"
                UPDATE users
                SET role_id = (
                    SELECT id
                    FROM system_roles
                    WHERE name = 'Content Developer'
                )
                WHERE id IN (
                    SELECT user_id
                    FROM user_permissions
                    WHERE permission_id = (
                        SELECT id
                        FROM permissions
                        WHERE key = 'ContentDeveloper'
                    )
                )
            ");
            // assign administrators
            migrationBuilder.Sql(@"
                UPDATE users
                SET role_id = (
                    SELECT id
                    FROM system_roles
                    WHERE name = 'Administrator'
                )
                WHERE id IN (
                    SELECT user_id
                    FROM user_permissions
                    WHERE permission_id = (
                        SELECT id
                        FROM permissions
                        WHERE key = 'SystemAdmin'
                    )
                )
            ");
            // assign evaluation observers
            migrationBuilder.Sql(@"
                INSERT INTO evaluation_memberships
                    (evaluation_id, user_id, group_id, role_id)
                    SELECT t.evaluation_id, tu.user_id, null, (select id from evaluation_roles where name = 'Observer')
	                    FROM team_users AS tu JOIN teams AS t ON tu.team_id = t.id
	                    WHERE tu.is_observer AND NOT tu.can_increment_move
            ");
            // assign evaluation advancers
            migrationBuilder.Sql(@"
                INSERT INTO evaluation_memberships
                    (evaluation_id, user_id, group_id, role_id)
                    SELECT t.evaluation_id, tu.user_id, null, (select id from evaluation_roles where name = 'Advancer')
	                    FROM team_users AS tu JOIN teams AS t ON tu.team_id = t.id
	                    WHERE NOT tu.is_observer AND tu.can_increment_move
            ");
            // assign evaluation observers
            migrationBuilder.Sql(@"
                INSERT INTO evaluation_memberships
                    (evaluation_id, user_id, group_id, role_id)
                    SELECT t.evaluation_id, tu.user_id, null, (select id from evaluation_roles where name = 'Observer')
	                    FROM team_users AS tu JOIN teams AS t ON tu.team_id = t.id
	                    WHERE tu.is_observer AND tu.can_increment_move
            ");
            // assign team members
            migrationBuilder.Sql(@"
                INSERT INTO team_memberships(team_id, user_id, role_id)
	                SELECT team_id, user_id, (select id from team_roles where name = 'Member')
	                    FROM team_users
	                    WHERE NOT can_manage_team AND NOT can_submit AND NOT can_modify
            ");
            // assign team contributors
            migrationBuilder.Sql(@"
                INSERT INTO team_memberships(team_id, user_id, role_id)
	                SELECT team_id, user_id, (select id from team_roles where name = 'Member')
	                    FROM team_users
	                    WHERE NOT can_manage_team AND NOT can_submit AND can_modify
            ");
            // assign team submitters
            migrationBuilder.Sql(@"
                INSERT INTO team_memberships(team_id, user_id, role_id)
	                SELECT team_id, user_id, (select id from team_roles where name = 'Member')
	                    FROM team_users
	                    WHERE NOT can_manage_team AND can_submit
            ");
            // assign team owners
            migrationBuilder.Sql(@"
                INSERT INTO team_memberships(team_id, user_id, role_id)
	                SELECT team_id, user_id, (select id from team_roles where name = 'Member')
	                    FROM team_users
	                    WHERE can_manage_team
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"UPDATE users SET role_id = null");

            migrationBuilder.Sql(@"
                DELETE FROM team_memberships
            ");

            migrationBuilder.Sql(@"
                DELETE FROM evaluation_memberships
            ");

            migrationBuilder.UpdateData(
                table: "scoring_model_roles",
                keyColumn: "id",
                keyValue: new Guid("39aa296e-05ba-4fb0-8d74-c92cf3354c60"),
                column: "name",
                value: "Observer");

            migrationBuilder.UpdateData(
                table: "system_roles",
                keyColumn: "id",
                keyValue: new Guid("1da3027e-725d-4753-9455-a836ed9bdb1e"),
                columns: new[] { "description", "name" },
                values: new object[] { "Can View all Evaluation Templates and Evaluations, but cannot make any changes.", "Observer" });

            migrationBuilder.UpdateData(
                table: "system_roles",
                keyColumn: "id",
                keyValue: new Guid("d80b73c3-95d7-4468-8650-c62bbd082507"),
                column: "description",
                value: "Can create and manage their own Evaluation Templates and Evaluations.");
        }
    }
}
