// Copyright 2022 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license, please see LICENSE.md in the project root for license information or contact permission@sei.cmu.edu for full terms.

using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cite.Api.Data.Models
{
    public class MoveEntity : BaseEntity
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public Guid Id { get; set; }
        public string Description { get; set; }
        public int MoveNumber { get; set; }
        public DateTime SituationTime { get; set;}
        public string SituationDescription { get; set; }
        public Guid EvaluationId { get; set; }
        public virtual EvaluationEntity Evaluation { get; set; }
    }
    public class MoveEntityConfiguration : IEntityTypeConfiguration<MoveEntity>
    {
        public void Configure(EntityTypeBuilder<MoveEntity> builder)
        {
            builder
                .HasIndex(move => new { move.EvaluationId, move.MoveNumber }).IsUnique();
        }
    }

}

