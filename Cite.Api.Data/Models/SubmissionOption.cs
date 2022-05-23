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
    public class SubmissionOptionEntity : BaseEntity
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public Guid Id { get; set; }
        public bool IsSelected { get; set; }
        public Guid SubmissionCategoryId { get; set; }
        public virtual SubmissionCategoryEntity SubmissionCategory { get; set; }
        public Guid ScoringOptionId { get; set; }
        public virtual ScoringOptionEntity ScoringOption { get; set; }
        public virtual ICollection<SubmissionCommentEntity> SubmissionComments { get; set; } = new HashSet<SubmissionCommentEntity>();
    }

    public class SubmissionOptionEntityConfiguration : IEntityTypeConfiguration<SubmissionOptionEntity>
    {
        public void Configure(EntityTypeBuilder<SubmissionOptionEntity> builder)
        {
            builder
                .HasOne(d => d.SubmissionCategory)
                .WithMany(d => d.SubmissionOptions)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }

}
