/*
Copyright 2021 Carnegie Mellon University. All Rights Reserved. 
 Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.
*/

﻿using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Cite.Api.Migrations.PostgreSQL.Migrations
{
    public partial class teamType : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "team_type_id",
                table: "teams",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "team_types",
                columns: table => new
                {
                    id = table.Column<Guid>(nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    date_created = table.Column<DateTime>(nullable: false),
                    date_modified = table.Column<DateTime>(nullable: true),
                    created_by = table.Column<Guid>(nullable: false),
                    modified_by = table.Column<Guid>(nullable: true),
                    name = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_team_types", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_teams_team_type_id",
                table: "teams",
                column: "team_type_id");

            migrationBuilder.AddForeignKey(
                name: "FK_teams_team_types_team_type_id",
                table: "teams",
                column: "team_type_id",
                principalTable: "team_types",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_teams_team_types_team_type_id",
                table: "teams");

            migrationBuilder.DropTable(
                name: "team_types");

            migrationBuilder.DropIndex(
                name: "IX_teams_team_type_id",
                table: "teams");

            migrationBuilder.DropColumn(
                name: "team_type_id",
                table: "teams");
        }
    }
}
