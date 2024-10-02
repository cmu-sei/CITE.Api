/*
 Copyright 2024 Carnegie Mellon University. All Rights Reserved.
 Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.
*/

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cite.Api.Migrations.PostgreSQL.Migrations
{
    public partial class Moveflagstoscoringmodel : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "display_comment_text_boxes",
                table: "evaluations");

            migrationBuilder.DropColumn(
                name: "display_scoring_model_by_move_number",
                table: "evaluations");

            migrationBuilder.DropColumn(
                name: "hide_scores_on_score_sheet",
                table: "evaluations");

            migrationBuilder.AddColumn<bool>(
                name: "display_comment_text_boxes",
                table: "scoring_models",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "display_scoring_model_by_move_number",
                table: "scoring_models",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "hide_scores_on_score_sheet",
                table: "scoring_models",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "display_comment_text_boxes",
                table: "scoring_models");

            migrationBuilder.DropColumn(
                name: "display_scoring_model_by_move_number",
                table: "scoring_models");

            migrationBuilder.DropColumn(
                name: "hide_scores_on_score_sheet",
                table: "scoring_models");

            migrationBuilder.AddColumn<bool>(
                name: "display_comment_text_boxes",
                table: "evaluations",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "display_scoring_model_by_move_number",
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
        }
    }
}
