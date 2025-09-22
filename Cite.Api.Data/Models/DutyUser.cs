// Copyright 2022 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license, please see LICENSE.md in the project root for license information or contact permission@sei.cmu.edu for full terms.

using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cite.Api.Data.Models
{
    public class DutyUserEntity
    {
        public DutyUserEntity() { }

        public DutyUserEntity(Guid userId, Guid dutyId)
        {
            UserId = userId;
            DutyId = dutyId;
        }

        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public Guid Id { get; set; }

        public Guid UserId { get; set; }
        public UserEntity User { get; set; }

        public Guid DutyId { get; set; }
        public DutyEntity Duty { get; set; }
    }

    public class DutyUserConfiguration : IEntityTypeConfiguration<DutyUserEntity>
    {
        public void Configure(EntityTypeBuilder<DutyUserEntity> builder)
        {
            builder.HasIndex(x => new { x.UserId, x.DutyId }).IsUnique();

            builder
                .HasOne(u => u.User)
                .WithMany(p => p.DutyUsers)
                .HasForeignKey(x => x.UserId);
            builder
                .HasOne(u => u.Duty)
                .WithMany(p => p.DutyUsers)
                .HasForeignKey(x => x.DutyId);
        }
    }
}
