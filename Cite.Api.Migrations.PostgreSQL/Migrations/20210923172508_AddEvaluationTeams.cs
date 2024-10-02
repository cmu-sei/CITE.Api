/*
Copyright 2021 Carnegie Mellon University. All Rights Reserved.
 Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.
*/

using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Cite.Api.Migrations.PostgreSQL.Migrations
{
    public partial class AddEvaluationTeams : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "evaluation_teams",
                columns: table => new
                {
                    id = table.Column<Guid>(nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    date_created = table.Column<DateTime>(nullable: false),
                    date_modified = table.Column<DateTime>(nullable: true),
                    created_by = table.Column<Guid>(nullable: false),
                    modified_by = table.Column<Guid>(nullable: true),
                    evaluation_id = table.Column<Guid>(nullable: false),
                    team_id = table.Column<Guid>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_evaluation_teams", x => x.id);
                    table.ForeignKey(
                        name: "FK_evaluation_teams_evaluations_evaluation_id",
                        column: x => x.evaluation_id,
                        principalTable: "evaluations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_evaluation_teams_teams_team_id",
                        column: x => x.team_id,
                        principalTable: "teams",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_evaluation_teams_team_id",
                table: "evaluation_teams",
                column: "team_id");

            migrationBuilder.CreateIndex(
                name: "IX_evaluation_teams_evaluation_id_team_id",
                table: "evaluation_teams",
                columns: new[] { "evaluation_id", "team_id" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "evaluation_teams");
        }
    }
}
