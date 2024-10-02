/*
 Copyright 2024 Carnegie Mellon University. All Rights Reserved.
 Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.
*/

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cite.Api.Migrations.PostgreSQL.Migrations
{
    public partial class Rightsidedata : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "right_side_embedded_url",
                table: "evaluations");

            migrationBuilder.DropColumn(
                name: "right_side_html_block",
                table: "evaluations");
        }
    }
}
