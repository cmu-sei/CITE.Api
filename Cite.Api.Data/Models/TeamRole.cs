// Copyright 2022 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license, please see LICENSE.md in the project root for license information or contact permission@sei.cmu.edu for full terms.

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Cite.Api.Data.Enumerations;

namespace Cite.Api.Data.Models
{
    public class TeamRoleEntity
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public Guid Id { get; set; }
        public string Name { get; set; }
        public virtual ICollection<TeamPermission> Permissions { get; set; } = new List<TeamPermission>();
    }

    public class TeamRoleConfiguration : IEntityTypeConfiguration<TeamRoleEntity>
    {
        public void Configure(EntityTypeBuilder<TeamRoleEntity> builder)
        {
            builder.HasIndex(e => new { e.Name }).IsUnique();
        }
    }
}
