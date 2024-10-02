/*
 Copyright 2024 Carnegie Mellon University. All Rights Reserved.
 Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.
*/

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cite.Api.Migrations.PostgreSQL.Migrations
{
    public partial class Scoringmodelflags : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "use_official_score",
                table: "scoring_models",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "use_submit",
                table: "scoring_models",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "use_team_average_score",
                table: "scoring_models",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "use_team_score",
                table: "scoring_models",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "use_type_average_score",
                table: "scoring_models",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "use_user_score",
                table: "scoring_models",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "use_official_score",
                table: "scoring_models");

            migrationBuilder.DropColumn(
                name: "use_submit",
                table: "scoring_models");

            migrationBuilder.DropColumn(
                name: "use_team_average_score",
                table: "scoring_models");

            migrationBuilder.DropColumn(
                name: "use_team_score",
                table: "scoring_models");

            migrationBuilder.DropColumn(
                name: "use_type_average_score",
                table: "scoring_models");

            migrationBuilder.DropColumn(
                name: "use_user_score",
                table: "scoring_models");
        }
    }
}
