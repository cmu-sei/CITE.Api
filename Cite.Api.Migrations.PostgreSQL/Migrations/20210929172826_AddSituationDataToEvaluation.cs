using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Cite.Api.Migrations.PostgreSQL.Migrations
{
    public partial class AddSituationDataToEvaluation : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "situation_description",
                table: "evaluations",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "situation_time",
                table: "evaluations",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "situation_description",
                table: "evaluations");

            migrationBuilder.DropColumn(
                name: "situation_time",
                table: "evaluations");
        }
    }
}
