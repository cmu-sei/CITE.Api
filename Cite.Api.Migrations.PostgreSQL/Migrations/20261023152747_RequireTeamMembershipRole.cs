using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cite.Api.Migrations.PostgreSQL.Migrations
{
    /// <inheritdoc />
    public partial class RequireTeamMembershipRole : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_team_memberships_team_roles_role_id",
                table: "team_memberships");

            // a membership without a role gets the Member role, as a new membership does
            migrationBuilder.Sql("UPDATE team_memberships SET role_id = 'b52ef031-65ee-4597-b768-b73480e6de67' WHERE role_id IS NULL;");

            migrationBuilder.AlterColumn<Guid>(
                name: "role_id",
                table: "team_memberships",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("b52ef031-65ee-4597-b768-b73480e6de67"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_team_memberships_team_roles_role_id",
                table: "team_memberships",
                column: "role_id",
                principalTable: "team_roles",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_team_memberships_team_roles_role_id",
                table: "team_memberships");

            migrationBuilder.AlterColumn<Guid>(
                name: "role_id",
                table: "team_memberships",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldDefaultValue: new Guid("b52ef031-65ee-4597-b768-b73480e6de67"));

            migrationBuilder.AddForeignKey(
                name: "FK_team_memberships_team_roles_role_id",
                table: "team_memberships",
                column: "role_id",
                principalTable: "team_roles",
                principalColumn: "id");
        }
    }
}
