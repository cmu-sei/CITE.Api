// Copyright 2022 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license, please see LICENSE.md in the project root for license information or contact permission@sei.cmu.edu for full terms.

using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cite.Api.Data.Models
{
    public class EvaluationTeamEntity : BaseEntity
    {
        public EvaluationTeamEntity() {}

        public EvaluationTeamEntity(Guid evaluationId, Guid teamId)
        {
            EvaluationId = evaluationId;
            TeamId = teamId;
        }

        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public Guid Id { get; set; }
        public Guid EvaluationId { get; set; }
        public EvaluationEntity Evaluation { get; set; }
        public Guid TeamId { get; set; }
        public TeamEntity Team { get; set; }
    }

    public class EvaluationTeamConfiguration : IEntityTypeConfiguration<EvaluationTeamEntity>
    {
        public void Configure(EntityTypeBuilder<EvaluationTeamEntity> builder)
        {
            builder.HasIndex(x => new { x.EvaluationId, x.TeamId }).IsUnique();
        }
    }
}
