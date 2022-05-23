// Copyright 2022 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license, please see LICENSE.md in the project root for license information or contact permission@sei.cmu.edu for full terms.

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure.Internal;
using Cite.Api.Data.Models;
using Cite.Api.Data.Extensions;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure.Internal;

namespace Cite.Api.Data
{
    public class CiteContext : DbContext
    {
        private DbContextOptions<CiteContext> _options;

        public CiteContext(DbContextOptions<CiteContext> options) : base(options) {
            _options = options;
        }

        public DbSet<ScoringModelEntity> ScoringModels { get; set; }
        public DbSet<ScoringCategoryEntity> ScoringCategories { get; set; }
        public DbSet<ScoringOptionEntity> ScoringOptions { get; set; }
        public DbSet<SubmissionEntity> Submissions { get; set; }
        public DbSet<SubmissionCategoryEntity> SubmissionCategories { get; set; }
        public DbSet<SubmissionOptionEntity> SubmissionOptions { get; set; }
        public DbSet<SubmissionCommentEntity> SubmissionComments { get; set; }
        public DbSet<UserEntity> Users { get; set; }
        public DbSet<PermissionEntity> Permissions { get; set; }
        public DbSet<UserPermissionEntity> UserPermissions { get; set; }
        public DbSet<TeamEntity> Teams { get; set; }
        public DbSet<TeamTypeEntity> TeamTypes { get; set; }
        public DbSet<TeamUserEntity> TeamUsers { get; set; }
        public DbSet<EvaluationEntity> Evaluations { get; set; }
        public DbSet<EvaluationTeamEntity> EvaluationTeams { get; set; }
        public DbSet<GroupEntity> Groups { get; set; }
        public DbSet<GroupTeamEntity> GroupTeams { get; set; }
        public DbSet<MoveEntity> Moves { get; set; }
        public DbSet<ActionEntity> Actions { get; set; }
        public DbSet<RoleEntity> Roles { get; set; }
        public DbSet<RoleUserEntity> RoleUsers { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfigurations();

            // Apply PostgreSQL specific options
            if (Database.IsNpgsql())
            {
                modelBuilder.AddPostgresUUIDGeneration();
                modelBuilder.UsePostgresCasing();
            }

        }
    }
}

