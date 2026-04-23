using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cite.Api.Migrations.PostgreSQL.Migrations
{
    /// <inheritdoc />
    public partial class addshowadvancebutton : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "show_advance_button",
                table: "evaluations",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // Set existing evaluations to show the advance button
            migrationBuilder.Sql("UPDATE evaluations SET show_advance_button = true");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "show_advance_button",
                table: "evaluations");
        }
    }
}
