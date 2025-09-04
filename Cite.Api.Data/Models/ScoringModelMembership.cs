// Copyright 2024 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cite.Api.Data.Models;

public class ScoringModelMembershipEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public Guid Id { get; set; }

    public Guid ScoringModelId { get; set; }
    public virtual ScoringModelEntity ScoringModel { get; set; }

    public Guid? UserId { get; set; }
    public virtual UserEntity User { get; set; }

    public Guid? GroupId { get; set; }
    public virtual GroupEntity Group { get; set; }

    public Guid RoleId { get; set; } = ScoringModelRoleEntityDefaults.ScoringModelMemberRoleId;
    public ScoringModelRoleEntity Role { get; set; }


    public ScoringModelMembershipEntity() { }

    public ScoringModelMembershipEntity(Guid scoringModelId, Guid? userId, Guid? groupId)
    {
        ScoringModelId = scoringModelId;
        UserId = userId;
        GroupId = groupId;
    }

    public class ScoringModelMembershipConfiguration : IEntityTypeConfiguration<ScoringModelMembershipEntity>
    {
        public void Configure(EntityTypeBuilder<ScoringModelMembershipEntity> builder)
        {
            builder.HasIndex(e => new { e.ScoringModelId, e.UserId, e.GroupId }).IsUnique();

            builder.Property(x => x.RoleId).HasDefaultValue(ScoringModelRoleEntityDefaults.ScoringModelMemberRoleId);

            builder
                .HasOne(x => x.ScoringModel)
                .WithMany(x => x.Memberships)
                .HasForeignKey(x => x.ScoringModelId);

            builder
                .HasOne(x => x.User)
                .WithMany(x => x.ScoringModelMemberships)
                .HasForeignKey(x => x.UserId)
                .HasPrincipalKey(x => x.Id);

            builder
                .HasOne(x => x.Group)
                .WithMany(x => x.ScoringModelMemberships)
                .HasForeignKey(x => x.GroupId)
                .HasPrincipalKey(x => x.Id);
        }
    }
}
