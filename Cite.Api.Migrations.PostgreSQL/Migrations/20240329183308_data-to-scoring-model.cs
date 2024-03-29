/*
 Copyright 2024 Carnegie Mellon University. All Rights Reserved. 
 Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.
*/

﻿using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cite.Api.Migrations.PostgreSQL.Migrations
{
    public partial class datatoscoringmodel : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "right_side_display",
                table: "evaluations");

            migrationBuilder.DropColumn(
                name: "right_side_embedded_url",
                table: "evaluations");

            migrationBuilder.DropColumn(
                name: "right_side_html_block",
                table: "evaluations");

            migrationBuilder.DropColumn(
                name: "show_past_situation_descriptions",
                table: "evaluations");

            migrationBuilder.AddColumn<int>(
                name: "right_side_display",
                table: "scoring_models",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "right_side_embedded_url",
                table: "scoring_models",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "right_side_html_block",
                table: "scoring_models",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "show_past_situation_descriptions",
                table: "scoring_models",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "right_side_display",
                table: "scoring_models");

            migrationBuilder.DropColumn(
                name: "right_side_embedded_url",
                table: "scoring_models");

            migrationBuilder.DropColumn(
                name: "right_side_html_block",
                table: "scoring_models");

            migrationBuilder.DropColumn(
                name: "show_past_situation_descriptions",
                table: "scoring_models");

            migrationBuilder.AddColumn<int>(
                name: "right_side_display",
                table: "evaluations",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "right_side_embedded_url",
                table: "evaluations",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "right_side_html_block",
                table: "evaluations",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "show_past_situation_descriptions",
                table: "evaluations",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }
    }
}
