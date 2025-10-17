// Copyright 2022 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license, please see LICENSE.md in the project root for license information or contact permission@sei.cmu.edu for full terms.

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Cite.Api.Data.Models
{
    public class TeamEntity : BaseEntity
    {
        [Key]
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string ShortName { get; set; }
        public Guid? EvaluationId { get; set; }
        public virtual EvaluationEntity Evaluation { get; set; }
        public Guid TeamTypeId { get; set; }
        public virtual TeamTypeEntity TeamType { get; set; }
        public ICollection<TeamUserEntity> TeamUsers { get; set; } = new List<TeamUserEntity>();
        public ICollection<TeamMembershipEntity> Memberships { get; set; } = new List<TeamMembershipEntity>();
        public ICollection<SubmissionEntity> Submissions { get; set; } = new List<SubmissionEntity>();
        public bool HideScoresheet { get; set; }
    }

    public class TeamConfiguration : IEntityTypeConfiguration<TeamEntity>
    {
        public void Configure(EntityTypeBuilder<TeamEntity> builder)
        {
            builder.HasIndex(e => e.Id).IsUnique();
        }
    }
}
