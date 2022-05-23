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
    public class SubmissionCommentEntity : BaseEntity
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public Guid Id { get; set; }
        public string Comment { get; set; }
        public Guid SubmissionOptionId { get; set; }
        public virtual SubmissionOptionEntity SubmissionOption { get; set; }
    }

    public class SubmissionCommentEntityConfiguration : IEntityTypeConfiguration<SubmissionCommentEntity>
    {
        public void Configure(EntityTypeBuilder<SubmissionCommentEntity> builder)
        {
            builder
                .HasOne(d => d.SubmissionOption)
                .WithMany(d => d.SubmissionComments)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }

}
