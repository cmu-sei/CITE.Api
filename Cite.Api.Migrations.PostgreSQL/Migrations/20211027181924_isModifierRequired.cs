/*
Copyright 2021 Carnegie Mellon University. All Rights Reserved. 
 Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.
*/

﻿using Microsoft.EntityFrameworkCore.Migrations;

namespace Cite.Api.Migrations.PostgreSQL.Migrations
{
    public partial class isModifierRequired : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "modifier_default_value",
                table: "scoring_categories");

            migrationBuilder.AddColumn<bool>(
                name: "is_modifier_required",
                table: "scoring_categories",
                nullable: false,
                defaultValue: false);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "is_modifier_required",
                table: "scoring_categories");

            migrationBuilder.AddColumn<double>(
                name: "modifier_default_value",
                table: "scoring_categories",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);
        }
    }
}
