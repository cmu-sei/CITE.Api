/*
 Copyright 2024 Carnegie Mellon University. All Rights Reserved.
 Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.
*/

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cite.Api.Migrations.PostgreSQL.Migrations
{
    public partial class Evaluationsettings : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "display_comment_text_boxes",
                table: "evaluations",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "hide_scores_on_score_sheet",
                table: "evaluations",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "right_side_display",
                table: "evaluations",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "show_past_situation_descriptions",
                table: "evaluations",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "display_comment_text_boxes",
                table: "evaluations");

            migrationBuilder.DropColumn(
                name: "hide_scores_on_score_sheet",
                table: "evaluations");

            migrationBuilder.DropColumn(
                name: "right_side_display",
                table: "evaluations");

            migrationBuilder.DropColumn(
                name: "show_past_situation_descriptions",
                table: "evaluations");
        }
    }
}
