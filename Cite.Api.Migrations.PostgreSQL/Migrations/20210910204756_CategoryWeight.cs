using Microsoft.EntityFrameworkCore.Migrations;

namespace Cite.Api.Migrations.PostgreSQL.Migrations
{
    public partial class CategoryWeight : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "calculation_method_id",
                table: "scoring_models",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<double>(
                name: "scoring_weight",
                table: "scoring_categories",
                nullable: false,
                defaultValue: 0.0);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "calculation_method_id",
                table: "scoring_models");

            migrationBuilder.DropColumn(
                name: "scoring_weight",
                table: "scoring_categories");
        }
    }
}
