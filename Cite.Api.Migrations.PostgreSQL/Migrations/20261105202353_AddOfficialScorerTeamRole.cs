// Copyright 2026 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.

﻿using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cite.Api.Migrations.PostgreSQL.Migrations
{
    /// <inheritdoc />
    public partial class AddOfficialScorerTeamRole : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "team_roles",
                columns: new[] { "id", "description", "name", "permissions" },
                values: new object[] { new Guid("1a88f86f-8e34-4bc2-b5b3-7777f70059f1"), "Can view the Team and score and submit the Official score for the Evaluation", "Official Scorer", new[] { 0, 4, 5, 6 } });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "team_roles",
                keyColumn: "id",
                keyValue: new Guid("1a88f86f-8e34-4bc2-b5b3-7777f70059f1"));
        }
    }
}
