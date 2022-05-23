using Microsoft.EntityFrameworkCore.Migrations;

namespace Cite.Api.Migrations.PostgreSQL.Migrations
{
    public partial class submission_unique_index : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
            @"
                DELETE from submissions
                where (date_created,
                COALESCE(evaluation_id, '00000000-0000-0000-0000-000000000000'),
                COALESCE(user_id, '00000000-0000-0000-0000-000000000000'),
                COALESCE(team_id, '00000000-0000-0000-0000-000000000000'),
                incident_number) not in (
                SELECT MIN(date_created) AS ""date_created"",
                COALESCE(evaluation_id, '00000000-0000-0000-0000-000000000000') as ""evaluation_id"",
                COALESCE(user_id, '00000000-0000-0000-0000-000000000000') as ""user_id"",
                COALESCE(team_id, '00000000-0000-0000-0000-000000000000') as ""team_id"",
                incident_number
                FROM submissions
                GROUP BY evaluation_id, user_id, team_id, incident_number
                );
            ");

            migrationBuilder.DropIndex(
                name: "IX_submissions_evaluation_id",
                table: "submissions");

            migrationBuilder.CreateIndex(
                name: "IX_submissions_evaluation_id_user_id_team_id_incident_number",
                table: "submissions",
                columns: new[] { "evaluation_id", "user_id", "team_id", "incident_number" },
                unique: true,
                filter: ""
                );
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_submissions_evaluation_id_user_id_team_id_incident_number",
                table: "submissions");

            migrationBuilder.CreateIndex(
                name: "IX_submissions_evaluation_id",
                table: "submissions",
                column: "evaluation_id");
        }
    }
}
