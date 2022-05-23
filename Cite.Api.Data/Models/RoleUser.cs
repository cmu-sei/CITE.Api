// Copyright 2022 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license, please see LICENSE.md in the project root for license information or contact permission@sei.cmu.edu for full terms.

using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cite.Api.Data.Models
{
    public class RoleUserEntity
    {
        public RoleUserEntity() { }

        public RoleUserEntity(Guid userId, Guid roleId)
        {
            UserId = userId;
            RoleId = roleId;
        }

        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public Guid Id { get; set; }

        public Guid UserId { get; set; }
        public UserEntity User { get; set; }

        public Guid RoleId { get; set; }
        public RoleEntity Role { get; set; }
    }

    public class RoleUserConfiguration : IEntityTypeConfiguration<RoleUserEntity>
    {
        public void Configure(EntityTypeBuilder<RoleUserEntity> builder)
        {
            builder.HasIndex(x => new { x.UserId, x.RoleId }).IsUnique();

            builder
                .HasOne(u => u.User)
                .WithMany(p => p.RoleUsers)
                .HasForeignKey(x => x.UserId);
            builder
                .HasOne(u => u.Role)
                .WithMany(p => p.RoleUsers)
                .HasForeignKey(x => x.RoleId);
        }
    }
}

