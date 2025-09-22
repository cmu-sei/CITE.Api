using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cite.Api.Migrations.PostgreSQL.Migrations
{
    /// <inheritdoc />
    public partial class roletoduty : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "role_id",
                table: "role_users",
                newName: "duty_id");
            migrationBuilder.RenameTable(
                name: "roles",
                newName: "duties");
            migrationBuilder.RenameTable(
                name: "role_users",
                newName: "duty_users");
         }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "duty_id",
                table: "duty_users",
                newName: "role_id");
            migrationBuilder.RenameTable(
                name: "duties",
                newName: "roles");
            migrationBuilder.RenameTable(
                name: "duty_users",
                newName: "role_users");
        }
    }
}
