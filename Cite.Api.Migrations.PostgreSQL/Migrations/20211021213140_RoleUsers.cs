using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Cite.Api.Migrations.PostgreSQL.Migrations
{
    public partial class RoleUsers : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_users_roles_role_entity_id",
                table: "users");

            migrationBuilder.DropIndex(
                name: "IX_users_role_entity_id",
                table: "users");

            migrationBuilder.DropColumn(
                name: "role_entity_id",
                table: "users");

            migrationBuilder.CreateTable(
                name: "role_users",
                columns: table => new
                {
                    id = table.Column<Guid>(nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    user_id = table.Column<Guid>(nullable: false),
                    role_id = table.Column<Guid>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_role_users", x => x.id);
                    table.ForeignKey(
                        name: "FK_role_users_roles_role_id",
                        column: x => x.role_id,
                        principalTable: "roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_role_users_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_role_users_role_id",
                table: "role_users",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "IX_role_users_user_id_role_id",
                table: "role_users",
                columns: new[] { "user_id", "role_id" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "role_users");

            migrationBuilder.AddColumn<Guid>(
                name: "role_entity_id",
                table: "users",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_role_entity_id",
                table: "users",
                column: "role_entity_id");

            migrationBuilder.AddForeignKey(
                name: "FK_users_roles_role_entity_id",
                table: "users",
                column: "role_entity_id",
                principalTable: "roles",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
