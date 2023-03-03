/*
 Copyright 2023 Carnegie Mellon University. All Rights Reserved. 
 Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.
*/

﻿using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cite.Api.Migrations.PostgreSQL.Migrations
{
    public partial class removefks : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_group_team_entity_group_entity_group_id",
                table: "group_team_entity");

            migrationBuilder.DropForeignKey(
                name: "FK_group_team_entity_teams_team_id",
                table: "group_team_entity");

            migrationBuilder.DropIndex(
                name: "IX_group_team_entity_group_id_team_id",
                table: "group_team_entity");

            migrationBuilder.DropIndex(
                name: "IX_group_team_entity_team_id",
                table: "group_team_entity");

            migrationBuilder.DropColumn(
                name: "group_id",
                table: "group_team_entity");

            migrationBuilder.DropColumn(
                name: "team_id",
                table: "group_team_entity");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "group_id",
                table: "group_team_entity",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "team_id",
                table: "group_team_entity",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_group_team_entity_group_id_team_id",
                table: "group_team_entity",
                columns: new[] { "group_id", "team_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_group_team_entity_team_id",
                table: "group_team_entity",
                column: "team_id");

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
        }
    }
}
