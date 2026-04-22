/*
 Copyright 2026 Carnegie Mellon University. All Rights Reserved.
 Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.
*/

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cite.Api.Migrations.PostgreSQL.Migrations
{
    public partial class CleanupOrphanedScoringModels : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // First, delete orphaned scoring models that reference non-existent evaluations
            // This is necessary before we can add the foreign key constraint
            migrationBuilder.Sql(@"
                DELETE FROM scoring_models
                WHERE evaluation_id IS NOT NULL
                AND NOT EXISTS (
                    SELECT 1 FROM evaluations e
                    WHERE e.id = scoring_models.evaluation_id
                );
            ");

            // Now add the foreign key constraint with CASCADE delete
            // When an evaluation is deleted, its associated scoring model will be automatically deleted
            migrationBuilder.AddForeignKey(
                name: "FK_scoring_models_evaluations_evaluation_id",
                table: "scoring_models",
                column: "evaluation_id",
                principalTable: "evaluations",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Remove the foreign key constraint
            migrationBuilder.DropForeignKey(
                name: "FK_scoring_models_evaluations_evaluation_id",
                table: "scoring_models");
        }
    }
}
