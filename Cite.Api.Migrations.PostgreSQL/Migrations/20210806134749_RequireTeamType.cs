using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Cite.Api.Migrations.PostgreSQL.Migrations
{
    public partial class RequireTeamType : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_teams_team_types_team_type_id",
                table: "teams");

            migrationBuilder.AlterColumn<Guid>(
                name: "team_type_id",
                table: "teams",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_teams_team_types_team_type_id",
                table: "teams",
                column: "team_type_id",
                principalTable: "team_types",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_teams_team_types_team_type_id",
                table: "teams");

            migrationBuilder.AlterColumn<Guid>(
                name: "team_type_id",
                table: "teams",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid));

            migrationBuilder.AddForeignKey(
                name: "FK_teams_team_types_team_type_id",
                table: "teams",
                column: "team_type_id",
                principalTable: "team_types",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
