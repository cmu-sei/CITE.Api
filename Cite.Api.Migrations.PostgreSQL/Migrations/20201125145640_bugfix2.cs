/*
Copyright 2021 Carnegie Mellon University. All Rights Reserved.
 Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.
*/

using Microsoft.EntityFrameworkCore.Migrations;

namespace Cite.Api.Migrations.PostgreSQL.Migrations
{
    public partial class Bugfix2 : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_submission_options_submission_categories_scoring_option_id",
                table: "submission_options");

            migrationBuilder.AddForeignKey(
                name: "FK_submission_options_scoring_options_scoring_option_id",
                table: "submission_options",
                column: "scoring_option_id",
                principalTable: "scoring_options",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_submission_options_scoring_options_scoring_option_id",
                table: "submission_options");

            migrationBuilder.AddForeignKey(
                name: "FK_submission_options_submission_categories_scoring_option_id",
                table: "submission_options",
                column: "scoring_option_id",
                principalTable: "submission_categories",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
