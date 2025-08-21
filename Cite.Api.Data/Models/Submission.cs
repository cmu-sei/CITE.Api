// Copyright 2022 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license, please see LICENSE.md in the project root for license information or contact permission@sei.cmu.edu for full terms.

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Cite.Api.Data.Enumerations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cite.Api.Data.Models
{
    public class SubmissionEntity : BaseEntity
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public Guid Id { get; set; }
        public double Score { get; set; }
        public ItemStatus Status { get; set; }
        public Guid ScoringModelId { get; set; }
        public virtual ScoringModelEntity ScoringModel { get; set; }
        public Guid? UserId { get; set; }
        public virtual UserEntity User { get; set; }
        public Guid? EvaluationId { get; set; }
        public virtual EvaluationEntity Evaluation { get; set; }
        public Guid? TeamId { get; set; }
        public virtual TeamEntity Team { get; set; }
        public int MoveNumber { get; set; }
        public virtual ICollection<SubmissionCategoryEntity> SubmissionCategories { get; set; } = new HashSet<SubmissionCategoryEntity>();
    }

    public class SubmissionEntityConfiguration : IEntityTypeConfiguration<SubmissionEntity>
    {
        public void Configure(EntityTypeBuilder<SubmissionEntity> builder)
        {
            builder
                .HasIndex(a => new { a.EvaluationId, a.UserId, a.TeamId, a.MoveNumber }).IsUnique().AreNullsDistinct(false);
            builder
                .HasOne(d => d.Evaluation)
                .WithMany(d => d.Submissions)
                .OnDelete(DeleteBehavior.Cascade);
            builder
                .HasOne(d => d.Team)
                .WithMany(d => d.Submissions)
                .OnDelete(DeleteBehavior.Cascade);
            builder
                .HasOne(d => d.User)
                .WithMany(d => d.Submissions)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }

}
