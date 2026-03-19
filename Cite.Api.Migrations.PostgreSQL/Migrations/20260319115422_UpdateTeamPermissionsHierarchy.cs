using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cite.Api.Migrations.PostgreSQL.Migrations
{
    /// <inheritdoc />
    public partial class UpdateTeamPermissionsHierarchy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "system_roles",
                keyColumn: "id",
                keyValue: new Guid("1da3027e-725d-4753-9455-a836ed9bdb1e"),
                column: "name",
                value: "Observer");

            migrationBuilder.UpdateData(
                table: "system_roles",
                keyColumn: "id",
                keyValue: new Guid("d80b73c3-95d7-4468-8650-c62bbd082507"),
                column: "permissions",
                value: new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 12, 14 });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "system_roles",
                keyColumn: "id",
                keyValue: new Guid("1da3027e-725d-4753-9455-a836ed9bdb1e"),
                column: "name",
                value: "Viewer");

            migrationBuilder.UpdateData(
                table: "system_roles",
                keyColumn: "id",
                keyValue: new Guid("d80b73c3-95d7-4468-8650-c62bbd082507"),
                column: "permissions",
                value: new[] { 0, 4 });
        }
    }
}
