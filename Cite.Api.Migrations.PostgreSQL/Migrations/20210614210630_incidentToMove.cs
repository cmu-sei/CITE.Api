/*
Copyright 2021 Carnegie Mellon University. All Rights Reserved.
 Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.
*/

using Microsoft.EntityFrameworkCore.Migrations;

namespace Cite.Api.Migrations.PostgreSQL.Migrations
{
    public partial class IncidentToMove : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
            @"
                update permissions
                set description='Can increment the current move',
                key='CanIncrementMove'
                where key='CanIncrementIncident';
            ");

            migrationBuilder.DropIndex(
                name: "IX_submissions_evaluation_id_user_id_team_id_incident_number",
                table: "submissions");

            migrationBuilder.RenameColumn(
                name: "incident_number",
                table: "submissions",
                newName: "move_number");

            migrationBuilder.RenameColumn(
                name: "current_incident_number",
                table: "evaluations",
                newName: "current_move_number");

            migrationBuilder.CreateIndex(
                name: "IX_submissions_evaluation_id_user_id_team_id_move_number",
                table: "submissions",
                columns: new[] { "evaluation_id", "user_id", "team_id", "move_number" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
            @"
                update permissions
                set description='Can increment the current incident',
                key='CanIncrementIncident'
                where key='CanIncrementMove';
            ");

            migrationBuilder.DropIndex(
                name: "IX_submissions_evaluation_id_user_id_team_id_move_number",
                table: "submissions");

            migrationBuilder.RenameColumn(
                name: "move_number",
                table: "submissions",
                newName: "incident_number");

            migrationBuilder.RenameColumn(
                name: "current_move_number",
                table: "evaluations",
                newName: "current_incident_number");

            migrationBuilder.CreateIndex(
                name: "IX_submissions_evaluation_id_user_id_team_id_incident_number",
                table: "submissions",
                columns: new[] { "evaluation_id", "user_id", "team_id", "incident_number" },
                unique: true);
        }
    }
}
