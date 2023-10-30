using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cite.Api.Migrations.PostgreSQL.Migrations
{
    public partial class teamtypeflags : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_official_score_contributor",
                table: "team_types",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "show_team_type_average",
                table: "team_types",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "is_official_score_contributor",
                table: "team_types");

            migrationBuilder.DropColumn(
                name: "show_team_type_average",
                table: "team_types");
        }
    }
}
