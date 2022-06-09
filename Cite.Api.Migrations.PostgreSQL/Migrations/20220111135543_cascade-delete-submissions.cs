/*
Copyright 2021 Carnegie Mellon University. All Rights Reserved. 
 Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.
*/

﻿using Microsoft.EntityFrameworkCore.Migrations;

namespace Cite.Api.Migrations.PostgreSQL.Migrations
{
    public partial class cascadedeletesubmissions : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_submissions_evaluations_evaluation_id",
                table: "submissions");

            migrationBuilder.DropForeignKey(
                name: "FK_submissions_teams_team_id",
                table: "submissions");

            migrationBuilder.DropForeignKey(
                name: "FK_submissions_users_user_id",
                table: "submissions");

            migrationBuilder.AddForeignKey(
                name: "FK_submissions_evaluations_evaluation_id",
                table: "submissions",
                column: "evaluation_id",
                principalTable: "evaluations",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_submissions_teams_team_id",
                table: "submissions",
                column: "team_id",
                principalTable: "teams",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_submissions_users_user_id",
                table: "submissions",
                column: "user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_submissions_evaluations_evaluation_id",
                table: "submissions");

            migrationBuilder.DropForeignKey(
                name: "FK_submissions_teams_team_id",
                table: "submissions");

            migrationBuilder.DropForeignKey(
                name: "FK_submissions_users_user_id",
                table: "submissions");

            migrationBuilder.AddForeignKey(
                name: "FK_submissions_evaluations_evaluation_id",
                table: "submissions",
                column: "evaluation_id",
                principalTable: "evaluations",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_submissions_teams_team_id",
                table: "submissions",
                column: "team_id",
                principalTable: "teams",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_submissions_users_user_id",
                table: "submissions",
                column: "user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
