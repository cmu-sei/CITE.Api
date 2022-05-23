// Copyright 2022 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license, please see LICENSE.md in the project root for license information or contact permission@sei.cmu.edu for full terms.

using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cite.Api.Data.Models
{
    public class GroupTeamEntity
    {
        public GroupTeamEntity() { }

        public GroupTeamEntity(Guid groupId, Guid teamId)
        {
            GroupId = groupId;
            TeamId = teamId;
        }

        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public Guid Id { get; set; }

        public Guid GroupId { get; set; }
        public GroupEntity Group { get; set; }

        public Guid TeamId { get; set; }
        public TeamEntity Team { get; set; }
    }

    public class GroupTeamConfiguration : IEntityTypeConfiguration<GroupTeamEntity>
    {
        public void Configure(EntityTypeBuilder<GroupTeamEntity> builder)
        {
            builder.HasIndex(x => new { x.GroupId, x.TeamId }).IsUnique();

            builder
                .HasOne(g => g.Group)
                .WithMany(p => p.GroupTeams)
                .HasForeignKey(x => x.GroupId);
            builder
                .HasOne(u => u.Team)
                .WithMany(p => p.GroupTeams)
                .HasForeignKey(x => x.TeamId);
        }
    }
}

