using Microsoft.EntityFrameworkCore.Migrations;

namespace Cite.Api.Migrations.PostgreSQL.Migrations
{
    public partial class bugfix1 : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_submission_categories_submissions_scoring_category_id",
                table: "submission_categories");

            migrationBuilder.AddForeignKey(
                name: "FK_submission_categories_scoring_categories_scoring_category_id",
                table: "submission_categories",
                column: "scoring_category_id",
                principalTable: "scoring_categories",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_submission_categories_scoring_categories_scoring_category_id",
                table: "submission_categories");

            migrationBuilder.AddForeignKey(
                name: "FK_submission_categories_submissions_scoring_category_id",
                table: "submission_categories",
                column: "scoring_category_id",
                principalTable: "submissions",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
