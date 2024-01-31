using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cite.Api.Migrations.PostgreSQL.Migrations
{
    public partial class categoriesbymove : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "move_number_first_display",
                table: "scoring_categories",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "move_number_last_display",
                table: "scoring_categories",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "display_scoring_model_by_move_number",
                table: "evaluations",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "move_number_first_display",
                table: "scoring_categories");

            migrationBuilder.DropColumn(
                name: "move_number_last_display",
                table: "scoring_categories");

            migrationBuilder.DropColumn(
                name: "display_scoring_model_by_move_number",
                table: "evaluations");
        }
    }
}
