// Copyright 2022 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license, please see LICENSE.md in the project root for license information or contact permission@sei.cmu.edu for full terms.

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Cite.Api.Data.Models
{
    public class UserEntity : BaseEntity
    {
        [Key]
        public Guid Id { get; set; }

        public string Name { get; set; }

        public ICollection<UserPermissionEntity> UserPermissions { get; set; } = new List<UserPermissionEntity>();
        public ICollection<TeamUserEntity> TeamUsers { get; set; } = new List<TeamUserEntity>();
        public ICollection<RoleUserEntity> RoleUsers { get; set; } = new HashSet<RoleUserEntity>();
        public ICollection<SubmissionEntity> Submissions { get; set; } = new List<SubmissionEntity>();
        public Guid? RoleId { get; set; }
        public virtual SystemRoleEntity Role { get; set; }

        public ICollection<EvaluationMembershipEntity> EvaluationMemberships { get; set; } = new List<EvaluationMembershipEntity>();
        public ICollection<ScoringModelMembershipEntity> ScoringModelMemberships { get; set; } = new List<ScoringModelMembershipEntity>();
        public ICollection<GroupMembershipEntity> GroupMemberships { get; set; } = new List<GroupMembershipEntity>();
    }

    public class UserConfiguration : IEntityTypeConfiguration<UserEntity>
    {
        public void Configure(EntityTypeBuilder<UserEntity> builder)
        {
            builder.HasIndex(e => e.Id).IsUnique();
        }
    }
}
