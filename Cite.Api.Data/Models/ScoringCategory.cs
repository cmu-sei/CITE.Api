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
    public class ScoringCategoryEntity : BaseEntity
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public Guid Id { get; set; }
        public int DisplayOrder { get; set; }
        public string Description { get; set; }
        public bool AllowMultipleChoices { get; set; }
        public string CalculationEquation { get; set; }
        public bool IsModifierRequired { get; set; }
        public double ScoringWeight { get; set; }
        public Guid ScoringModelId { get; set; }
        public virtual ScoringModelEntity ScoringModel { get; set; }
        public virtual ICollection<ScoringOptionEntity> ScoringOptions { get; set; } = new HashSet<ScoringOptionEntity>();
    }

    public class ScoringCategoryEntityConfiguration : IEntityTypeConfiguration<ScoringCategoryEntity>
    {
        public void Configure(EntityTypeBuilder<ScoringCategoryEntity> builder)
        {
            builder
                .HasOne(d => d.ScoringModel)
                .WithMany(d => d.ScoringCategories)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }

}
