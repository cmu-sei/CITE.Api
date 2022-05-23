using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Cite.Api.Migrations.PostgreSQL.Migrations
{
    public partial class ActionsAndRoles : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "role_entity_id",
                table: "users",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "actions",
                columns: table => new
                {
                    id = table.Column<Guid>(nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    date_created = table.Column<DateTime>(nullable: false),
                    date_modified = table.Column<DateTime>(nullable: true),
                    created_by = table.Column<Guid>(nullable: false),
                    modified_by = table.Column<Guid>(nullable: true),
                    evaluation_id = table.Column<Guid>(nullable: false),
                    team_id = table.Column<Guid>(nullable: false),
                    move_number = table.Column<int>(nullable: false),
                    inject_number = table.Column<int>(nullable: false),
                    action_number = table.Column<int>(nullable: false),
                    description = table.Column<string>(nullable: true),
                    is_checked = table.Column<bool>(nullable: false),
                    changed_by = table.Column<Guid>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_actions", x => x.id);
                    table.ForeignKey(
                        name: "FK_actions_evaluations_evaluation_id",
                        column: x => x.evaluation_id,
                        principalTable: "evaluations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_actions_teams_team_id",
                        column: x => x.team_id,
                        principalTable: "teams",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "roles",
                columns: table => new
                {
                    id = table.Column<Guid>(nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    date_created = table.Column<DateTime>(nullable: false),
                    date_modified = table.Column<DateTime>(nullable: true),
                    created_by = table.Column<Guid>(nullable: false),
                    modified_by = table.Column<Guid>(nullable: true),
                    evaluation_id = table.Column<Guid>(nullable: false),
                    team_id = table.Column<Guid>(nullable: false),
                    name = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_roles", x => x.id);
                    table.ForeignKey(
                        name: "FK_roles_evaluations_evaluation_id",
                        column: x => x.evaluation_id,
                        principalTable: "evaluations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_roles_teams_team_id",
                        column: x => x.team_id,
                        principalTable: "teams",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_users_role_entity_id",
                table: "users",
                column: "role_entity_id");

            migrationBuilder.CreateIndex(
                name: "IX_actions_evaluation_id",
                table: "actions",
                column: "evaluation_id");

            migrationBuilder.CreateIndex(
                name: "IX_actions_team_id",
                table: "actions",
                column: "team_id");

            migrationBuilder.CreateIndex(
                name: "IX_roles_evaluation_id",
                table: "roles",
                column: "evaluation_id");

            migrationBuilder.CreateIndex(
                name: "IX_roles_team_id",
                table: "roles",
                column: "team_id");

            migrationBuilder.AddForeignKey(
                name: "FK_users_roles_role_entity_id",
                table: "users",
                column: "role_entity_id",
                principalTable: "roles",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_users_roles_role_entity_id",
                table: "users");

            migrationBuilder.DropTable(
                name: "actions");

            migrationBuilder.DropTable(
                name: "roles");

            migrationBuilder.DropIndex(
                name: "IX_users_role_entity_id",
                table: "users");

            migrationBuilder.DropColumn(
                name: "role_entity_id",
                table: "users");
        }
    }
}
