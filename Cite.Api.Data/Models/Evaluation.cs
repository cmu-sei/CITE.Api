// Copyright 2025 Carnegie Mellon University. All Rights Reserved.
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
    public class EvaluationEntity : BaseEntity
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public Guid Id { get; set; }
        public string Description { get; set; }
        public ItemStatus Status { get; set; }
        public int CurrentMoveNumber { get; set; }
        public DateTime SituationTime { get; set; }
        public string SituationDescription { get; set; }
        public Guid ScoringModelId { get; set; }
        public virtual ScoringModelEntity ScoringModel { get; set; }
        public Guid? GalleryExhibitId { get; set; }
        public bool ShowAdvanceButton { get; set; }
        public virtual ICollection<TeamEntity> Teams { get; set; } = new HashSet<TeamEntity>();
        public virtual ICollection<MoveEntity> Moves { get; set; } = new HashSet<MoveEntity>();
        public ICollection<SubmissionEntity> Submissions { get; set; } = new List<SubmissionEntity>();
        public virtual ICollection<EvaluationMembershipEntity> Memberships { get; set; } = new List<EvaluationMembershipEntity>();
    }

    public class EvaluationEntityConfiguration : IEntityTypeConfiguration<EvaluationEntity>
    {
        public void Configure(EntityTypeBuilder<EvaluationEntity> builder)
        {
            // Evaluation references its ScoringModel copy via ScoringModelId (Evaluation is dependent)
            builder.HasOne(e => e.ScoringModel)
                .WithMany()
                .HasForeignKey(e => e.ScoringModelId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }

    public class ScoringModelEvaluationConfiguration : IEntityTypeConfiguration<ScoringModelEntity>
    {
        public void Configure(EntityTypeBuilder<ScoringModelEntity> builder)
        {
            // ScoringModel belongs to an Evaluation via EvaluationId — cascade delete when evaluation is deleted
            builder.HasOne<EvaluationEntity>()
                .WithMany()
                .HasForeignKey(sm => sm.EvaluationId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
