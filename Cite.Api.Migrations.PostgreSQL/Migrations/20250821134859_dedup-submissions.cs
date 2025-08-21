using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cite.Api.Migrations.PostgreSQL.Migrations
{
    /// <inheritdoc />
    public partial class dedupsubmissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "delete from submissions where id in " +
                "(SELECT id from " +
                "( select id, evaluation_id, move_number, team_id, user_id, ROW_NUMBER() " +
                "OVER(PARTITION BY evaluation_id, move_number, team_id, user_id ORDER BY date_created desc) " +
                "AS row_num FROM submissions) as t " +
                "where t.row_num > 1)"
            );

            migrationBuilder.DropIndex(
                name: "IX_submissions_evaluation_id_user_id_team_id_move_number",
                table: "submissions");

            // NOTE:  This requires PostgreSQL version 15 or greater
            migrationBuilder.CreateIndex(
                name: "IX_submissions_evaluation_id_user_id_team_id_move_number",
                table: "submissions",
                columns: new[] { "evaluation_id", "user_id", "team_id", "move_number" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_submissions_evaluation_id_user_id_team_id_move_number",
                table: "submissions");

            migrationBuilder.CreateIndex(
                name: "IX_submissions_evaluation_id_user_id_team_id_move_number",
                table: "submissions",
                columns: new[] { "evaluation_id", "user_id", "team_id", "move_number" },
                unique: true);
        }
    }
}
