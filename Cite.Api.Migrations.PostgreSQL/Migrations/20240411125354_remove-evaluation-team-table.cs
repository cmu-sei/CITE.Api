using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cite.Api.Migrations.PostgreSQL.Migrations
{
    public partial class removeevaluationteamtable : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_evaluation_teams_evaluations_evaluation_id",
                table: "evaluation_teams");

            migrationBuilder.DropForeignKey(
                name: "FK_evaluation_teams_teams_team_id",
                table: "evaluation_teams");

            migrationBuilder.DropIndex(
                name: "IX_evaluation_teams_evaluation_id_team_id",
                table: "evaluation_teams");

            migrationBuilder.DropIndex(
                name: "IX_evaluation_teams_team_id",
                table: "evaluation_teams");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_evaluation_teams_evaluation_id_team_id",
                table: "evaluation_teams",
                columns: new[] { "evaluation_id", "team_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_evaluation_teams_team_id",
                table: "evaluation_teams",
                column: "team_id");

            migrationBuilder.AddForeignKey(
                name: "FK_evaluation_teams_evaluations_evaluation_id",
                table: "evaluation_teams",
                column: "evaluation_id",
                principalTable: "evaluations",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_evaluation_teams_teams_team_id",
                table: "evaluation_teams",
                column: "team_id",
                principalTable: "teams",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
