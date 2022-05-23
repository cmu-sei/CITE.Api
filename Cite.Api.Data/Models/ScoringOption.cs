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
    public class ScoringOptionEntity : BaseEntity
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public Guid Id { get; set; }
        public int DisplayOrder { get; set; }
        public string Description { get; set; }
        public bool IsModifier { get; set; }
        public double Value { get; set; }
        public Guid ScoringCategoryId { get; set; }
        public virtual ScoringCategoryEntity ScoringCategory { get; set; }
    }

    public class ScoringOptionEntityConfiguration : IEntityTypeConfiguration<ScoringOptionEntity>
    {
        public void Configure(EntityTypeBuilder<ScoringOptionEntity> builder)
        {
            builder
                .HasOne(d => d.ScoringCategory)
                .WithMany(d => d.ScoringOptions)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }

}
