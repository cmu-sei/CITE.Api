// Copyright 2022 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license, please see LICENSE.md in the project root for license information or contact permission@sei.cmu.edu for full terms.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Cite.Api.Data.Models;
using Cite.Api.Data.Extensions;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Cite.Api.Data
{
    public class CiteContext : DbContext
    {
        // Needed for EventInterceptor
        public IServiceProvider ServiceProvider;

        // Entity Events collected by EventTransactionInterceptor and published in SaveChanges
        public List<INotification> Events { get; } = [];

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
        public DbSet<DutyEntity> Duties { get; set; }
        public DbSet<DutyUserEntity> DutyUsers { get; set; }
        public DbSet<SystemRoleEntity> SystemRoles { get; set; }
        public DbSet<EvaluationRoleEntity> EvaluationRoles { get; set; }
        public DbSet<EvaluationMembershipEntity> EvaluationMemberships { get; set; }
        public DbSet<ScoringModelRoleEntity> ScoringModelRoles { get; set; }
        public DbSet<ScoringModelMembershipEntity> ScoringModelMemberships { get; set; }
        public DbSet<TeamRoleEntity> TeamRoles { get; set; }
        public DbSet<TeamMembershipEntity> TeamMemberships { get; set; }
        public DbSet<GroupEntity> Groups { get; set; }
        public DbSet<GroupMembershipEntity> GroupMemberships { get; set; }
        public DbSet<XApiQueuedStatementEntity> XApiQueuedStatements { get; set; }

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

        public override int SaveChanges()
        {
            HandleAuditFields();
            var result = base.SaveChanges();
            PublishEvents().Wait();
            return result;
        }

        public override async Task<int> SaveChangesAsync(CancellationToken ct = default)
        {
            HandleAuditFields();
            var result = await base.SaveChangesAsync(ct);
            await PublishEvents(ct);
            return result;
        }

        private async Task PublishEvents(CancellationToken cancellationToken = default)
        {
            // Publish deferred events after transaction is committed and cleared
            if (Events.Count > 0 && ServiceProvider is not null)
            {
                var mediator = ServiceProvider.GetRequiredService<IMediator>();
                var eventsToPublish = Events.ToArray();
                Events.Clear();

                foreach (var evt in eventsToPublish)
                {
                    await mediator.Publish(evt, cancellationToken);
                }
            }
        }

        /// <summary>
        /// Handle audit fields (DateCreated, DateModified, CreatedBy, ModifiedBy)
        /// </summary>
        private void HandleAuditFields()
        {
            // Handle audit fields for added entries
            var addedEntries = ChangeTracker.Entries().Where(x => x.State == EntityState.Added);
            foreach (var entry in addedEntries)
            {
                try
                {
                    ((BaseEntity)entry.Entity).DateCreated = DateTime.UtcNow;
                    ((BaseEntity)entry.Entity).DateModified = null;
                    ((BaseEntity)entry.Entity).ModifiedBy = null;
                }
                catch
                { }
            }

            // Handle audit fields for modified entries
            var modifiedEntries = ChangeTracker.Entries().Where(x => x.State == EntityState.Modified);
            foreach (var entry in modifiedEntries)
            {
                try
                {
                    ((BaseEntity)entry.Entity).DateModified = DateTime.UtcNow;
                    ((BaseEntity)entry.Entity).CreatedBy = (Guid)entry.OriginalValues["CreatedBy"];
                    ((BaseEntity)entry.Entity).DateCreated = DateTime.SpecifyKind((DateTime)entry.OriginalValues["DateCreated"], DateTimeKind.Utc);
                }
                catch
                { }
            }
        }

    }
}
