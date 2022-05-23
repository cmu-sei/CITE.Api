// Copyright 2022 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license, please see LICENSE.md in the project root for license information or contact permission@sei.cmu.edu for full terms.

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cite.Api.Data.Models
{
    public class SubmissionCategoryEntity : BaseEntity
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public Guid Id { get; set; }
        public double Score { get; set; }
        public Guid SubmissionId { get; set; }
        public virtual SubmissionEntity Submission { get; set; }
        public Guid ScoringCategoryId { get; set; }
        public virtual ScoringCategoryEntity ScoringCategory { get; set; }
        public virtual ICollection<SubmissionOptionEntity> SubmissionOptions { get; set; } = new HashSet<SubmissionOptionEntity>();
    }

    public class SubmissionCategoryEntityConfiguration : IEntityTypeConfiguration<SubmissionCategoryEntity>
    {
        public void Configure(EntityTypeBuilder<SubmissionCategoryEntity> builder)
        {
            builder
                .HasOne(d => d.Submission)
                .WithMany(d => d.SubmissionCategories)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }

}
