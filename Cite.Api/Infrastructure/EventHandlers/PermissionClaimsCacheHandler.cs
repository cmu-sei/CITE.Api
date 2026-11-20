// Copyright 2026 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.

using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Crucible.Common.Authentication.Claims;
using Crucible.Common.EntityEvents.Events;
using Cite.Api.Data.Models;

namespace Cite.Api.Infrastructure.EventHandlers
{
    /// <summary>
    /// Drops cached permission claims (see <see cref="Services.UserClaimsService"/>) when a saved change alters what
    /// they would be, so the change applies on this pod at once rather than when the entry expires. Other pods still
    /// wait out <c>ClaimsTransformation:CacheExpirationSeconds</c>.
    /// </summary>
    /// <remarks>
    /// A change to a user's row evicts that user; <c>ValidateUser</c> saves that row from inside the claims load, so
    /// evicting everyone from there would be noisy. Every membership, group and role change evicts everyone.
    /// </remarks>
    public class PermissionClaimsCacheHandler(IPermissionClaimsCache cache) :
        INotificationHandler<EntityUpdated<UserEntity>>,
        INotificationHandler<EntityDeleted<UserEntity>>,
        INotificationHandler<EntityCreated<TeamMembershipEntity>>,
        INotificationHandler<EntityUpdated<TeamMembershipEntity>>,
        INotificationHandler<EntityDeleted<TeamMembershipEntity>>,
        INotificationHandler<EntityCreated<EvaluationMembershipEntity>>,
        INotificationHandler<EntityUpdated<EvaluationMembershipEntity>>,
        INotificationHandler<EntityDeleted<EvaluationMembershipEntity>>,
        INotificationHandler<EntityCreated<ScoringModelMembershipEntity>>,
        INotificationHandler<EntityUpdated<ScoringModelMembershipEntity>>,
        INotificationHandler<EntityDeleted<ScoringModelMembershipEntity>>,
        INotificationHandler<EntityCreated<GroupMembershipEntity>>,
        INotificationHandler<EntityUpdated<GroupMembershipEntity>>,
        INotificationHandler<EntityDeleted<GroupMembershipEntity>>,
        INotificationHandler<EntityUpdated<GroupEntity>>,
        INotificationHandler<EntityDeleted<GroupEntity>>,
        INotificationHandler<EntityCreated<SystemRoleEntity>>,
        INotificationHandler<EntityUpdated<SystemRoleEntity>>,
        INotificationHandler<EntityDeleted<SystemRoleEntity>>,
        INotificationHandler<EntityUpdated<EvaluationRoleEntity>>,
        INotificationHandler<EntityDeleted<EvaluationRoleEntity>>,
        INotificationHandler<EntityUpdated<ScoringModelRoleEntity>>,
        INotificationHandler<EntityDeleted<ScoringModelRoleEntity>>,
        INotificationHandler<EntityUpdated<TeamRoleEntity>>,
        INotificationHandler<EntityDeleted<TeamRoleEntity>>
    {
        // ---- users: the system role, and a deleted user's row (recreated on their next load)

        // every update evicts: services save users with Update(), which marks every property modified, so the
        // modified properties cannot tell a system role change from a rename
        public Task Handle(EntityUpdated<UserEntity> notification, CancellationToken cancellationToken) =>
            Invalidate(notification.Entity.Id);

        public Task Handle(EntityDeleted<UserEntity> notification, CancellationToken cancellationToken) =>
            Invalidate(notification.Entity.Id);

        // ---- memberships, groups and roles: permission changes are rare during an exercise, so each one drops
        // every cached entry rather than working out who holds it. A group's members include whoever the identity
        // provider puts in a group of that name, so the holder cannot always be listed anyway.

        public Task Handle(EntityCreated<TeamMembershipEntity> notification, CancellationToken cancellationToken) => InvalidateAll();

        public Task Handle(EntityUpdated<TeamMembershipEntity> notification, CancellationToken cancellationToken) => InvalidateAll();

        public Task Handle(EntityDeleted<TeamMembershipEntity> notification, CancellationToken cancellationToken) => InvalidateAll();

        public Task Handle(EntityCreated<EvaluationMembershipEntity> notification, CancellationToken cancellationToken) => InvalidateAll();

        public Task Handle(EntityUpdated<EvaluationMembershipEntity> notification, CancellationToken cancellationToken) => InvalidateAll();

        public Task Handle(EntityDeleted<EvaluationMembershipEntity> notification, CancellationToken cancellationToken) => InvalidateAll();

        public Task Handle(EntityCreated<ScoringModelMembershipEntity> notification, CancellationToken cancellationToken) => InvalidateAll();

        public Task Handle(EntityUpdated<ScoringModelMembershipEntity> notification, CancellationToken cancellationToken) => InvalidateAll();

        public Task Handle(EntityDeleted<ScoringModelMembershipEntity> notification, CancellationToken cancellationToken) => InvalidateAll();

        public Task Handle(EntityCreated<GroupMembershipEntity> notification, CancellationToken cancellationToken) => InvalidateAll();

        public Task Handle(EntityUpdated<GroupMembershipEntity> notification, CancellationToken cancellationToken) => InvalidateAll();

        public Task Handle(EntityDeleted<GroupMembershipEntity> notification, CancellationToken cancellationToken) => InvalidateAll();

        public Task Handle(EntityUpdated<GroupEntity> notification, CancellationToken cancellationToken) => InvalidateAll();

        public Task Handle(EntityDeleted<GroupEntity> notification, CancellationToken cancellationToken) => InvalidateAll();

        // ---- roles. A new system role can already be held, through a realm role of the same name; a new role of any
        // other kind is held by nobody until a membership names it.

        public Task Handle(EntityCreated<SystemRoleEntity> notification, CancellationToken cancellationToken) => InvalidateAll();

        public Task Handle(EntityUpdated<SystemRoleEntity> notification, CancellationToken cancellationToken) => InvalidateAll();

        public Task Handle(EntityDeleted<SystemRoleEntity> notification, CancellationToken cancellationToken) => InvalidateAll();

        public Task Handle(EntityUpdated<EvaluationRoleEntity> notification, CancellationToken cancellationToken) => InvalidateAll();

        public Task Handle(EntityDeleted<EvaluationRoleEntity> notification, CancellationToken cancellationToken) => InvalidateAll();

        public Task Handle(EntityUpdated<ScoringModelRoleEntity> notification, CancellationToken cancellationToken) => InvalidateAll();

        public Task Handle(EntityDeleted<ScoringModelRoleEntity> notification, CancellationToken cancellationToken) => InvalidateAll();

        public Task Handle(EntityUpdated<TeamRoleEntity> notification, CancellationToken cancellationToken) => InvalidateAll();

        public Task Handle(EntityDeleted<TeamRoleEntity> notification, CancellationToken cancellationToken) => InvalidateAll();

        private Task Invalidate(System.Guid userId)
        {
            cache.Invalidate(userId);
            return Task.CompletedTask;
        }

        private Task InvalidateAll()
        {
            cache.InvalidateAll();
            return Task.CompletedTask;
        }
    }
}
