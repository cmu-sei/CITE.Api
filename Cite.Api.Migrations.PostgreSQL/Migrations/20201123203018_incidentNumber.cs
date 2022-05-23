// Copyright 2022 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license, please see LICENSE.md in the project root for license information or contact permission@sei.cmu.edu for full terms.

using Microsoft.EntityFrameworkCore.Migrations;

namespace Cite.Api.Migrations.PostgreSQL.Migrations
{
    public partial class incidentNumber : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_scoring_categories_scoring_models_scoring_model_id",
                table: "scoring_categories");

            migrationBuilder.DropForeignKey(
                name: "FK_scoring_options_scoring_categories_scoring_category_id",
                table: "scoring_options");

            migrationBuilder.AddColumn<int>(
                name: "incident_number",
                table: "submissions",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "current_incident_number",
                table: "evaluations",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddForeignKey(
                name: "FK_scoring_categories_scoring_models_scoring_model_id",
                table: "scoring_categories",
                column: "scoring_model_id",
                principalTable: "scoring_models",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_scoring_options_scoring_categories_scoring_category_id",
                table: "scoring_options",
                column: "scoring_category_id",
                principalTable: "scoring_categories",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_scoring_categories_scoring_models_scoring_model_id",
                table: "scoring_categories");

            migrationBuilder.DropForeignKey(
                name: "FK_scoring_options_scoring_categories_scoring_category_id",
                table: "scoring_options");

            migrationBuilder.DropColumn(
                name: "incident_number",
                table: "submissions");

            migrationBuilder.DropColumn(
                name: "current_incident_number",
                table: "evaluations");

            migrationBuilder.AddForeignKey(
                name: "FK_scoring_categories_scoring_models_scoring_model_id",
                table: "scoring_categories",
                column: "scoring_model_id",
                principalTable: "scoring_models",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_scoring_options_scoring_categories_scoring_category_id",
                table: "scoring_options",
                column: "scoring_category_id",
                principalTable: "scoring_categories",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
