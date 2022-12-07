/*
 Copyright 2022 Carnegie Mellon University. All Rights Reserved. 
 Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.
*/

﻿using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cite.Api.Migrations.PostgreSQL.Migrations
{
    public partial class moveSituationTimeAndDescription : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "situation_description",
                table: "evaluations");

            migrationBuilder.DropColumn(
                name: "situation_time",
                table: "evaluations");

            migrationBuilder.AddColumn<string>(
                name: "situation_description",
                table: "moves",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "situation_time",
                table: "moves",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "situation_description",
                table: "moves");

            migrationBuilder.DropColumn(
                name: "situation_time",
                table: "moves");

            migrationBuilder.AddColumn<string>(
                name: "situation_description",
                table: "evaluations",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "situation_time",
                table: "evaluations",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));
        }
    }
}
