/*
 Copyright 2023 Carnegie Mellon University. All Rights Reserved. 
 Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.
*/

﻿using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cite.Api.Migrations.PostgreSQL.Migrations
{
    public partial class teanevaluationid : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_group_teams_groups_group_id",
                table: "group_teams");

            migrationBuilder.DropForeignKey(
                name: "FK_group_teams_teams_team_id",
                table: "group_teams");

            migrationBuilder.DropForeignKey(
                name: "FK_teams_evaluations_evaluation_entity_id",
                table: "teams");

            migrationBuilder.DropPrimaryKey(
                name: "PK_groups",
                table: "groups");

            migrationBuilder.DropPrimaryKey(
                name: "PK_group_teams",
                table: "group_teams");

            migrationBuilder.RenameTable(
                name: "groups",
                newName: "group_entity");

            migrationBuilder.RenameTable(
                name: "group_teams",
                newName: "group_team_entity");

            migrationBuilder.RenameColumn(
                name: "evaluation_entity_id",
                table: "teams",
                newName: "evaluation_id");

            migrationBuilder.RenameIndex(
                name: "IX_teams_evaluation_entity_id",
                table: "teams",
                newName: "IX_teams_evaluation_id");

            migrationBuilder.RenameIndex(
                name: "IX_groups_id",
                table: "group_entity",
                newName: "IX_group_entity_id");

            migrationBuilder.RenameIndex(
                name: "IX_group_teams_team_id",
                table: "group_team_entity",
                newName: "IX_group_team_entity_team_id");

            migrationBuilder.RenameIndex(
                name: "IX_group_teams_group_id_team_id",
                table: "group_team_entity",
                newName: "IX_group_team_entity_group_id_team_id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_group_entity",
                table: "group_entity",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_group_team_entity",
                table: "group_team_entity",
                column: "id");

            migrationBuilder.AddForeignKey(
                name: "FK_group_team_entity_group_entity_group_id",
                table: "group_team_entity",
                column: "group_id",
                principalTable: "group_entity",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_group_team_entity_teams_team_id",
                table: "group_team_entity",
                column: "team_id",
                principalTable: "teams",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_teams_evaluations_evaluation_id",
                table: "teams",
                column: "evaluation_id",
                principalTable: "evaluations",
                principalColumn: "id");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_group_team_entity_group_entity_group_id",
                table: "group_team_entity");

            migrationBuilder.DropForeignKey(
                name: "FK_group_team_entity_teams_team_id",
                table: "group_team_entity");

            migrationBuilder.DropForeignKey(
                name: "FK_teams_evaluations_evaluation_id",
                table: "teams");

            migrationBuilder.DropPrimaryKey(
                name: "PK_group_team_entity",
                table: "group_team_entity");

            migrationBuilder.DropPrimaryKey(
                name: "PK_group_entity",
                table: "group_entity");

            migrationBuilder.RenameTable(
                name: "group_team_entity",
                newName: "group_teams");

            migrationBuilder.RenameTable(
                name: "group_entity",
                newName: "groups");

            migrationBuilder.RenameColumn(
                name: "evaluation_id",
                table: "teams",
                newName: "evaluation_entity_id");

            migrationBuilder.RenameIndex(
                name: "IX_teams_evaluation_id",
                table: "teams",
                newName: "IX_teams_evaluation_entity_id");

            migrationBuilder.RenameIndex(
                name: "IX_group_team_entity_team_id",
                table: "group_teams",
                newName: "IX_group_teams_team_id");

            migrationBuilder.RenameIndex(
                name: "IX_group_team_entity_group_id_team_id",
                table: "group_teams",
                newName: "IX_group_teams_group_id_team_id");

            migrationBuilder.RenameIndex(
                name: "IX_group_entity_id",
                table: "groups",
                newName: "IX_groups_id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_group_teams",
                table: "group_teams",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_groups",
                table: "groups",
                column: "id");

            migrationBuilder.AddForeignKey(
                name: "FK_group_teams_groups_group_id",
                table: "group_teams",
                column: "group_id",
                principalTable: "groups",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_group_teams_teams_team_id",
                table: "group_teams",
                column: "team_id",
                principalTable: "teams",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_teams_evaluations_evaluation_entity_id",
                table: "teams",
                column: "evaluation_entity_id",
                principalTable: "evaluations",
                principalColumn: "id");
        }
    }
}
