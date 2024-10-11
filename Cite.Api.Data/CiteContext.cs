// Copyright 2022 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license, please see LICENSE.md in the project root for license information or contact permission@sei.cmu.edu for full terms.

using System;
using Microsoft.EntityFrameworkCore;
using Cite.Api.Data.Models;
using Cite.Api.Data.Extensions;

namespace Cite.Api.Data
{
    public class CiteContext : DbContext
    {
        // Needed for EventInterceptor
        public IServiceProvider ServiceProvider;

        public CiteContext(DbContextOptions<CiteContext> options) : base(options) { }

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
