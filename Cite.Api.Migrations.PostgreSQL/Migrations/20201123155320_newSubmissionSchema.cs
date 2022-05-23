// Copyright 2022 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license, please see LICENSE.md in the project root for license information or contact permission@sei.cmu.edu for full terms.

using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Cite.Api.Migrations.PostgreSQL.Migrations
{
    public partial class newSubmissionSchema : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_submission_categories_submissions_submission_id",
                table: "submission_categories");

            migrationBuilder.DropForeignKey(
                name: "FK_submission_options_submission_categories_submission_categor~",
                table: "submission_options");

            migrationBuilder.DropForeignKey(
                name: "FK_submissions_evaluations_evaluation_id",
                table: "submissions");

            migrationBuilder.DropForeignKey(
                name: "FK_submissions_teams_team_id",
                table: "submissions");

            migrationBuilder.DropColumn(
                name: "description",
                table: "submissions");

            migrationBuilder.DropColumn(
                name: "description",
                table: "submission_options");

            migrationBuilder.DropColumn(
                name: "display_order",
                table: "submission_options");

            migrationBuilder.DropColumn(
                name: "value",
                table: "submission_options");

            migrationBuilder.DropColumn(
                name: "calculation_method_id",
                table: "submission_categories");

            migrationBuilder.DropColumn(
                name: "description",
                table: "submission_categories");

            migrationBuilder.DropColumn(
                name: "display_order",
                table: "submission_categories");

            migrationBuilder.AlterColumn<Guid>(
                name: "team_id",
                table: "submissions",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<Guid>(
                name: "evaluation_id",
                table: "submissions",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "scoring_option_id",
                table: "submission_options",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "scoring_category_id",
                table: "submission_categories",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_submission_options_scoring_option_id",
                table: "submission_options",
                column: "scoring_option_id");

            migrationBuilder.CreateIndex(
                name: "IX_submission_categories_scoring_category_id",
                table: "submission_categories",
                column: "scoring_category_id");

            migrationBuilder.AddForeignKey(
                name: "FK_submission_categories_submissions_scoring_category_id",
                table: "submission_categories",
                column: "scoring_category_id",
                principalTable: "submissions",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_submission_categories_submissions_submission_id",
                table: "submission_categories",
                column: "submission_id",
                principalTable: "submissions",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_submission_options_submission_categories_scoring_option_id",
                table: "submission_options",
                column: "scoring_option_id",
                principalTable: "submission_categories",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_submission_options_submission_categories_submission_categor~",
                table: "submission_options",
                column: "submission_category_id",
                principalTable: "submission_categories",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_submissions_evaluations_evaluation_id",
                table: "submissions",
                column: "evaluation_id",
                principalTable: "evaluations",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_submissions_teams_team_id",
                table: "submissions",
                column: "team_id",
                principalTable: "teams",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_submission_categories_submissions_scoring_category_id",
                table: "submission_categories");

            migrationBuilder.DropForeignKey(
                name: "FK_submission_categories_submissions_submission_id",
                table: "submission_categories");

            migrationBuilder.DropForeignKey(
                name: "FK_submission_options_submission_categories_scoring_option_id",
                table: "submission_options");

            migrationBuilder.DropForeignKey(
                name: "FK_submission_options_submission_categories_submission_categor~",
                table: "submission_options");

            migrationBuilder.DropForeignKey(
                name: "FK_submissions_evaluations_evaluation_id",
                table: "submissions");

            migrationBuilder.DropForeignKey(
                name: "FK_submissions_teams_team_id",
                table: "submissions");

            migrationBuilder.DropIndex(
                name: "IX_submission_options_scoring_option_id",
                table: "submission_options");

            migrationBuilder.DropIndex(
                name: "IX_submission_categories_scoring_category_id",
                table: "submission_categories");

            migrationBuilder.DropColumn(
                name: "scoring_option_id",
                table: "submission_options");

            migrationBuilder.DropColumn(
                name: "scoring_category_id",
                table: "submission_categories");

            migrationBuilder.AlterColumn<Guid>(
                name: "team_id",
                table: "submissions",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "evaluation_id",
                table: "submissions",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "description",
                table: "submissions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "description",
                table: "submission_options",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "display_order",
                table: "submission_options",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "value",
                table: "submission_options",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "calculation_method_id",
                table: "submission_categories",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "description",
                table: "submission_categories",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "display_order",
                table: "submission_categories",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddForeignKey(
                name: "FK_submission_categories_submissions_submission_id",
                table: "submission_categories",
                column: "submission_id",
                principalTable: "submissions",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_submission_options_submission_categories_submission_categor~",
                table: "submission_options",
                column: "submission_category_id",
                principalTable: "submission_categories",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_submissions_evaluations_evaluation_id",
                table: "submissions",
                column: "evaluation_id",
                principalTable: "evaluations",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_submissions_teams_team_id",
                table: "submissions",
                column: "team_id",
                principalTable: "teams",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
