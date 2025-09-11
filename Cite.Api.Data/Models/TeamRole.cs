// Copyright 2022 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license, please see LICENSE.md in the project root for license information or contact permission@sei.cmu.edu for full terms.

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Cite.Api.Data.Enumerations;

namespace Cite.Api.Data.Models
{
    public class TeamRoleEntity
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public virtual ICollection<TeamPermission> Permissions { get; set; } = new List<TeamPermission>();
    }

public static class TeamRoleDefaults
{
    public static Guid TeamMemberRoleId = new("b52ef031-65ee-4597-b768-b73480e6de66");
    public static Guid TeamContributorRoleId = new("c442cf49-e26e-45c5-be7c-00e710d2e054");
    public static Guid TeamSubmitterRoleId = new("1cfce79f-f344-4cb1-b33a-55de8dc1ccb2");
    public static Guid TeamManagerRoleId = new("a2cc11c1-9fd1-402b-9937-0f6ede1066c2");
}

public class TeamRoleConfiguration : IEntityTypeConfiguration<TeamRoleEntity>
{
    public void Configure(EntityTypeBuilder<TeamRoleEntity> builder)
    {
        builder.HasData(
            new TeamRoleEntity
            {
                Id = TeamRoleDefaults.TeamManagerRoleId,
                Name = "Manager",
                Permissions = [
                    TeamPermission.ViewTeam,
                    TeamPermission.EditTeamScore,
                    TeamPermission.SubmitTeamScore,
                    TeamPermission.ManageTeam
                ],
                Description = "Can perform all actions for the Team"
            },
            new TeamRoleEntity
            {
                Id = TeamRoleDefaults.TeamSubmitterRoleId,
                Name = "Submitter",
                Permissions = [
                    TeamPermission.ViewTeam,
                    TeamPermission.EditTeamScore,
                    TeamPermission.SubmitTeamScore
                ],
                Description = "Can contribute to and submit the answers for the Team"
            },
            new TeamRoleEntity
            {
                Id = TeamRoleDefaults.TeamContributorRoleId,
                Name = "Contributor",
                Permissions = [
                    TeamPermission.ViewTeam,
                    TeamPermission.EditTeamScore
                ],
                Description = "Can contribute answers for the Team"
            },
            new TeamRoleEntity
            {
                Id = TeamRoleDefaults.TeamMemberRoleId,
                Name = "Member",
                Permissions = [
                    TeamPermission.ViewTeam
                ],
                Description = "Has read only access to the Team"
            }
        );
    }
}
}
