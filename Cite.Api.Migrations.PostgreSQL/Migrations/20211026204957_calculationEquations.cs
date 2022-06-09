/*
Copyright 2021 Carnegie Mellon University. All Rights Reserved. 
 Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.
*/

﻿using Microsoft.EntityFrameworkCore.Migrations;

namespace Cite.Api.Migrations.PostgreSQL.Migrations
{
    public partial class calculationEquations : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "calculation_method_id",
                table: "scoring_models");

            migrationBuilder.DropColumn(
                name: "calculation_method_id",
                table: "scoring_categories");

            migrationBuilder.AddColumn<string>(
                name: "calculation_equation",
                table: "scoring_models",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "calculation_equation",
                table: "scoring_categories",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "calculation_equation",
                table: "scoring_models");

            migrationBuilder.DropColumn(
                name: "calculation_equation",
                table: "scoring_categories");

            migrationBuilder.AddColumn<int>(
                name: "calculation_method_id",
                table: "scoring_models",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "calculation_method_id",
                table: "scoring_categories",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }
    }
}
