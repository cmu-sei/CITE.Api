using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cite.Api.Migrations.PostgreSQL.Migrations
{
    /// <inheritdoc />
    public partial class cascadedeletescoringmodel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_evaluations_scoring_models_scoring_model_id",
                table: "evaluations");

            // Clean up orphaned scoring models that reference non-existent evaluations
            migrationBuilder.Sql(
                @"DELETE FROM scoring_models
                  WHERE evaluation_id IS NOT NULL
                    AND evaluation_id NOT IN (SELECT id FROM evaluations)");

            migrationBuilder.CreateIndex(
                name: "IX_scoring_models_evaluation_id",
                table: "scoring_models",
                column: "evaluation_id");

            migrationBuilder.AddForeignKey(
                name: "FK_evaluations_scoring_models_scoring_model_id",
                table: "evaluations",
                column: "scoring_model_id",
                principalTable: "scoring_models",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_scoring_models_evaluations_evaluation_id",
                table: "scoring_models",
                column: "evaluation_id",
                principalTable: "evaluations",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_evaluations_scoring_models_scoring_model_id",
                table: "evaluations");

            migrationBuilder.DropForeignKey(
                name: "FK_scoring_models_evaluations_evaluation_id",
                table: "scoring_models");

            migrationBuilder.DropIndex(
                name: "IX_scoring_models_evaluation_id",
                table: "scoring_models");

            migrationBuilder.AddForeignKey(
                name: "FK_evaluations_scoring_models_scoring_model_id",
                table: "evaluations",
                column: "scoring_model_id",
                principalTable: "scoring_models",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
