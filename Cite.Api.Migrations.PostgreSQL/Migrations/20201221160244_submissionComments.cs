/*
Copyright 2021 Carnegie Mellon University. All Rights Reserved.
 Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.
*/

using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Cite.Api.Migrations.PostgreSQL.Migrations
{
    public partial class SubmissionComments : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "submission_comments",
                columns: table => new
                {
                    id = table.Column<Guid>(nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    date_created = table.Column<DateTime>(nullable: false),
                    date_modified = table.Column<DateTime>(nullable: true),
                    created_by = table.Column<Guid>(nullable: false),
                    modified_by = table.Column<Guid>(nullable: true),
                    comment = table.Column<string>(nullable: true),
                    submission_option_id = table.Column<Guid>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_submission_comments", x => x.id);
                    table.ForeignKey(
                        name: "FK_submission_comments_submission_options_submission_option_id",
                        column: x => x.submission_option_id,
                        principalTable: "submission_options",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_submission_comments_submission_option_id",
                table: "submission_comments",
                column: "submission_option_id");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "submission_comments");
        }
    }
}
