// Copyright 2024 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cite.Api.Data.Models;

public class EvaluationMembershipEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public Guid Id { get; set; }

    public Guid EvaluationId { get; set; }
    public virtual EvaluationEntity Evaluation { get; set; }

    public Guid? UserId { get; set; }
    public virtual UserEntity User { get; set; }

    public Guid? GroupId { get; set; }
    public virtual GroupEntity Group { get; set; }

    public Guid RoleId { get; set; } = EvaluationRoleDefaults.EvaluationMemberRoleId;
    public EvaluationRoleEntity Role { get; set; }


    public EvaluationMembershipEntity() { }

    public EvaluationMembershipEntity(Guid evaluationId, Guid? userId, Guid? groupId)
    {
        EvaluationId = evaluationId;
        UserId = userId;
        GroupId = groupId;
    }

    public class EvaluationMembershipEntityConfiguration : IEntityTypeConfiguration<EvaluationMembershipEntity>
    {
        public void Configure(EntityTypeBuilder<EvaluationMembershipEntity> builder)
        {
            builder.HasIndex(e => new { e.EvaluationId, e.UserId, e.GroupId }).IsUnique();

            builder.Property(x => x.RoleId).HasDefaultValue(EvaluationRoleDefaults.EvaluationMemberRoleId);

            builder
                .HasOne(x => x.Evaluation)
                .WithMany(x => x.Memberships)
                .HasForeignKey(x => x.EvaluationId);

            builder
                .HasOne(x => x.User)
                .WithMany(x => x.EvaluationMemberships)
                .HasForeignKey(x => x.UserId)
                .HasPrincipalKey(x => x.Id);

            builder
                .HasOne(x => x.Group)
                .WithMany(x => x.EvaluationMemberships)
                .HasForeignKey(x => x.GroupId)
                .HasPrincipalKey(x => x.Id);
        }
    }
}
