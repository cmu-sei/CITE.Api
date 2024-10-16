/*
 Copyright 2023 Carnegie Mellon University. All Rights Reserved.
 Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.
*/

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cite.Api.Migrations.PostgreSQL.Migrations
{
    public partial class Teamcascadedelete : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_teams_evaluations_evaluation_id",
                table: "teams");

            migrationBuilder.AddForeignKey(
                name: "FK_teams_evaluations_evaluation_id",
                table: "teams",
                column: "evaluation_id",
                principalTable: "evaluations",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_teams_evaluations_evaluation_id",
                table: "teams");

            migrationBuilder.AddForeignKey(
                name: "FK_teams_evaluations_evaluation_id",
                table: "teams",
                column: "evaluation_id",
                principalTable: "evaluations",
                principalColumn: "id");
        }
    }
}
