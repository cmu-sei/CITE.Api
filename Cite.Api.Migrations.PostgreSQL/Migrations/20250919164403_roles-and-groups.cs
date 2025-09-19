using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Cite.Api.Migrations.PostgreSQL.Migrations
{
    /// <inheritdoc />
    public partial class rolesandgroups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "role_id",
                table: "users",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "evaluation_roles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    name = table.Column<string>(type: "text", nullable: true),
                    description = table.Column<string>(type: "text", nullable: true),
                    all_permissions = table.Column<bool>(type: "boolean", nullable: false),
                    permissions = table.Column<int[]>(type: "integer[]", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_evaluation_roles", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "groups",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    name = table.Column<string>(type: "text", nullable: true),
                    description = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_groups", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "scoring_model_roles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    name = table.Column<string>(type: "text", nullable: true),
                    description = table.Column<string>(type: "text", nullable: true),
                    all_permissions = table.Column<bool>(type: "boolean", nullable: false),
                    permissions = table.Column<int[]>(type: "integer[]", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_scoring_model_roles", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "system_roles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    name = table.Column<string>(type: "text", nullable: true),
                    description = table.Column<string>(type: "text", nullable: true),
                    all_permissions = table.Column<bool>(type: "boolean", nullable: false),
                    immutable = table.Column<bool>(type: "boolean", nullable: false),
                    permissions = table.Column<int[]>(type: "integer[]", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_system_roles", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "team_roles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    name = table.Column<string>(type: "text", nullable: true),
                    description = table.Column<string>(type: "text", nullable: true),
                    permissions = table.Column<int[]>(type: "integer[]", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_team_roles", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "evaluation_memberships",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    evaluation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    group_id = table.Column<Guid>(type: "uuid", nullable: true),
                    role_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValue: new Guid("f870d8ee-7332-4f7f-8ee0-63bd07cfd7e6"))
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_evaluation_memberships", x => x.id);
                    table.ForeignKey(
                        name: "FK_evaluation_memberships_evaluation_roles_role_id",
                        column: x => x.role_id,
                        principalTable: "evaluation_roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_evaluation_memberships_evaluations_evaluation_id",
                        column: x => x.evaluation_id,
                        principalTable: "evaluations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_evaluation_memberships_groups_group_id",
                        column: x => x.group_id,
                        principalTable: "groups",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_evaluation_memberships_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "group_memberships",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    group_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_group_memberships", x => x.id);
                    table.ForeignKey(
                        name: "FK_group_memberships_groups_group_id",
                        column: x => x.group_id,
                        principalTable: "groups",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_group_memberships_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "scoring_model_memberships",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    scoring_model_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    group_id = table.Column<Guid>(type: "uuid", nullable: true),
                    role_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValue: new Guid("f870d8ee-7332-4f7f-8ee0-63bd07cfd7e5"))
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_scoring_model_memberships", x => x.id);
                    table.ForeignKey(
                        name: "FK_scoring_model_memberships_groups_group_id",
                        column: x => x.group_id,
                        principalTable: "groups",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_scoring_model_memberships_scoring_model_roles_role_id",
                        column: x => x.role_id,
                        principalTable: "scoring_model_roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_scoring_model_memberships_scoring_models_scoring_model_id",
                        column: x => x.scoring_model_id,
                        principalTable: "scoring_models",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_scoring_model_memberships_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "team_memberships",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    team_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_team_memberships", x => x.id);
                    table.ForeignKey(
                        name: "FK_team_memberships_team_roles_role_id",
                        column: x => x.role_id,
                        principalTable: "team_roles",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_team_memberships_teams_team_id",
                        column: x => x.team_id,
                        principalTable: "teams",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_team_memberships_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "evaluation_roles",
                columns: new[] { "id", "all_permissions", "description", "name", "permissions" },
                values: new object[,]
                {
                    { new Guid("1a3f26cd-9d99-4b98-b914-12931e78619a"), true, "Can perform all actions on the Evaluation in administration", "Owner", new int[0] },
                    { new Guid("39aa296e-05ba-4fb0-8d74-c92cf3354c61"), false, "Has read only access to all teams in the Evaluation up to the current move", "Observer", new[] { 4 } },
                    { new Guid("4af74a62-596c-4767-a43a-b74aa8e48527"), false, "Can view the Evaluation in administration", "Viewer", new[] { 0 } },
                    { new Guid("7c366199-f795-4a04-b360-e8705e77a053"), false, "Can observe all teams and advance moves for the Evaluation", "Facilitator", new[] { 4, 3 } },
                    { new Guid("8aaa0d30-bdbe-4f2b-a6b8-f1a5466b2561"), false, "Can edit the Evaluation in administration", "Editor", new[] { 0, 1 } },
                    { new Guid("c1e1731c-d44a-4b6b-abc1-2e3626798304"), false, "Can advance moves for the Evaluation", "Advancer", new[] { 3 } },
                    { new Guid("f870d8ee-7332-4f7f-8ee0-63bd07cfd7e6"), false, "Has read only access to the Evaluation up to the current move", "Member", new[] { 5 } }
                });

            migrationBuilder.InsertData(
                table: "scoring_model_roles",
                columns: new[] { "id", "all_permissions", "description", "name", "permissions" },
                values: new object[,]
                {
                    { new Guid("1a3f26cd-9d99-4b98-b914-12931e786199"), true, "Can view, edit, and delete the ScoringModel", "Owner", new int[0] },
                    { new Guid("39aa296e-05ba-4fb0-8d74-c92cf3354c60"), false, "Can view the ScoringModel", "Observer", new[] { 0 } },
                    { new Guid("f870d8ee-7332-4f7f-8ee0-63bd07cfd7e5"), false, "Can view and edit the ScoringModel", "Editor", new[] { 0, 1 } }
                });

            migrationBuilder.InsertData(
                table: "system_roles",
                columns: new[] { "id", "all_permissions", "description", "immutable", "name", "permissions" },
                values: new object[,]
                {
                    { new Guid("1da3027e-725d-4753-9455-a836ed9bdb1e"), false, "Can View all Evaluation Templates and Evaluations, but cannot make any changes.", false, "Observer", new[] { 1, 5, 10, 12, 14 } },
                    { new Guid("d80b73c3-95d7-4468-8650-c62bbd082507"), false, "Can create and manage their own Evaluation Templates and Evaluations.", false, "Content Developer", new[] { 0, 4 } },
                    { new Guid("f35e8fff-f996-4cba-b303-3ba515ad8d2f"), true, "Can perform all actions", true, "Administrator", new int[0] }
                });

            migrationBuilder.InsertData(
                table: "team_roles",
                columns: new[] { "id", "description", "name", "permissions" },
                values: new object[,]
                {
                    { new Guid("1cfce79f-f344-4cb1-b33a-55de8dc1ccb3"), "Can contribute to and submit the answers for the Team", "Submitter", new[] { 0, 1, 2 } },
                    { new Guid("a2cc11c1-9fd1-402b-9937-0f6ede1066c3"), "Can perform all actions for the Team", "Owner", new[] { 0, 1, 2, 3 } },
                    { new Guid("b52ef031-65ee-4597-b768-b73480e6de67"), "Has read only access to the Team", "Member", new[] { 0 } },
                    { new Guid("c442cf49-e26e-45c5-be7c-00e710d2e055"), "Can contribute answers for the Team", "Contributor", new[] { 0, 1 } }
                });

            migrationBuilder.CreateIndex(
                name: "IX_users_role_id",
                table: "users",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "IX_evaluation_memberships_evaluation_id_user_id_group_id",
                table: "evaluation_memberships",
                columns: new[] { "evaluation_id", "user_id", "group_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_evaluation_memberships_group_id",
                table: "evaluation_memberships",
                column: "group_id");

            migrationBuilder.CreateIndex(
                name: "IX_evaluation_memberships_role_id",
                table: "evaluation_memberships",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "IX_evaluation_memberships_user_id",
                table: "evaluation_memberships",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_group_memberships_group_id_user_id",
                table: "group_memberships",
                columns: new[] { "group_id", "user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_group_memberships_user_id",
                table: "group_memberships",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_scoring_model_memberships_group_id",
                table: "scoring_model_memberships",
                column: "group_id");

            migrationBuilder.CreateIndex(
                name: "IX_scoring_model_memberships_role_id",
                table: "scoring_model_memberships",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "IX_scoring_model_memberships_scoring_model_id_user_id_group_id",
                table: "scoring_model_memberships",
                columns: new[] { "scoring_model_id", "user_id", "group_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_scoring_model_memberships_user_id",
                table: "scoring_model_memberships",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_system_roles_name",
                table: "system_roles",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_team_memberships_role_id",
                table: "team_memberships",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "IX_team_memberships_team_id_user_id",
                table: "team_memberships",
                columns: new[] { "team_id", "user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_team_memberships_user_id",
                table: "team_memberships",
                column: "user_id");

            migrationBuilder.AddForeignKey(
                name: "FK_users_system_roles_role_id",
                table: "users",
                column: "role_id",
                principalTable: "system_roles",
                principalColumn: "id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_users_system_roles_role_id",
                table: "users");

            migrationBuilder.DropTable(
                name: "evaluation_memberships");

            migrationBuilder.DropTable(
                name: "group_memberships");

            migrationBuilder.DropTable(
                name: "scoring_model_memberships");

            migrationBuilder.DropTable(
                name: "system_roles");

            migrationBuilder.DropTable(
                name: "team_memberships");

            migrationBuilder.DropTable(
                name: "evaluation_roles");

            migrationBuilder.DropTable(
                name: "groups");

            migrationBuilder.DropTable(
                name: "scoring_model_roles");

            migrationBuilder.DropTable(
                name: "team_roles");

            migrationBuilder.DropIndex(
                name: "IX_users_role_id",
                table: "users");

            migrationBuilder.DropColumn(
                name: "role_id",
                table: "users");
        }
    }
}
