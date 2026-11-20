// Copyright 2026 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.

using System.Security.Claims;
using Crucible.Common.Authentication.Claims;
using Crucible.Common.EntityEvents.Events;
using Cite.Api.Data.Models;
using Cite.Api.Infrastructure.EventHandlers;
using Cite.Api.Tests.Support;

namespace Cite.Api.Tests.Infrastructure.EventHandlers;

/// <summary>
/// Which saved changes drop whose cached permission claims. A change to one user's row evicts that user; every
/// membership, group and role change evicts everyone; anything else leaves the cache alone.
/// </summary>
public class PermissionClaimsCacheHandlerTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly RecordingCache _cache = new();
    private PermissionClaimsCacheHandler Handler => new(_cache);

    [Fact]
    public async Task Giving_a_user_another_system_role_evicts_that_user()
    {
        var user = TestData.User();

        await Handler.Handle(new EntityUpdated<UserEntity>(user, [nameof(UserEntity.RoleId)]), Ct);

        Assert.Equal([user.Id], _cache.Invalidated);
        Assert.False(_cache.InvalidatedAll);
    }

    /// <summary>
    /// Users are saved with <c>Update()</c>, which marks every property modified, so a rename cannot be told from a
    /// role change; any update evicts.
    /// </summary>
    [Fact]
    public async Task Any_update_to_a_user_evicts_that_user()
    {
        var user = TestData.User();

        await Handler.Handle(new EntityUpdated<UserEntity>(user, [nameof(UserEntity.Name)]), Ct);

        Assert.Equal([user.Id], _cache.Invalidated);
        Assert.False(_cache.InvalidatedAll);
    }

    [Fact]
    public async Task Deleting_a_user_evicts_that_user_so_their_row_is_recreated_on_the_next_request()
    {
        var user = TestData.User();

        await Handler.Handle(new EntityDeleted<UserEntity>(user), Ct);

        Assert.Equal([user.Id], _cache.Invalidated);
    }

    [Fact]
    public async Task Any_membership_change_evicts_everyone()
    {
        var team = TestData.TeamMembership(Guid.NewGuid(), Guid.NewGuid());
        var group = TestData.GroupMembership(Guid.NewGuid(), Guid.NewGuid());
        var evaluation = TestData.EvaluationMembership(Guid.NewGuid(), Guid.NewGuid(), TestData.EvaluationRoles.Member);
        var heldByGroup = TestData.EvaluationMembership(Guid.NewGuid(), null, TestData.EvaluationRoles.Member, Guid.NewGuid());
        var scoringModel = TestData.ScoringModelMembership(Guid.NewGuid(), Guid.NewGuid(), TestData.ScoringModelRoles.Editor);

        Func<Task>[] changes =
        [
            () => Handler.Handle(new EntityCreated<TeamMembershipEntity>(team), Ct),
            () => Handler.Handle(new EntityUpdated<TeamMembershipEntity>(team, [nameof(TeamMembershipEntity.RoleId)]), Ct),
            () => Handler.Handle(new EntityDeleted<TeamMembershipEntity>(team), Ct),
            () => Handler.Handle(new EntityCreated<GroupMembershipEntity>(group), Ct),
            () => Handler.Handle(new EntityUpdated<GroupMembershipEntity>(group, [nameof(GroupMembershipEntity.UserId)]), Ct),
            () => Handler.Handle(new EntityDeleted<GroupMembershipEntity>(group), Ct),
            () => Handler.Handle(new EntityCreated<EvaluationMembershipEntity>(evaluation), Ct),
            () => Handler.Handle(new EntityUpdated<EvaluationMembershipEntity>(evaluation, [nameof(EvaluationMembershipEntity.RoleId)]), Ct),
            () => Handler.Handle(new EntityDeleted<EvaluationMembershipEntity>(heldByGroup), Ct),
            () => Handler.Handle(new EntityCreated<ScoringModelMembershipEntity>(scoringModel), Ct),
            () => Handler.Handle(new EntityUpdated<ScoringModelMembershipEntity>(scoringModel, [nameof(ScoringModelMembershipEntity.UserId)]), Ct),
            () => Handler.Handle(new EntityDeleted<ScoringModelMembershipEntity>(scoringModel), Ct),
        ];

        foreach (var change in changes)
        {
            _cache.InvalidatedAll = false;
            await change();
            Assert.True(_cache.InvalidatedAll);
        }

        Assert.Empty(_cache.Invalidated);
    }

    [Fact]
    public async Task Any_group_update_or_delete_evicts_everyone()
    {
        var group = TestData.Group();

        await Handler.Handle(new EntityUpdated<GroupEntity>(group, [nameof(GroupEntity.Description)]), Ct);
        Assert.True(_cache.InvalidatedAll);

        _cache.InvalidatedAll = false;
        await Handler.Handle(new EntityDeleted<GroupEntity>(group), Ct);
        Assert.True(_cache.InvalidatedAll);
    }

    /// <summary>A new system role is held at once by everyone with a realm role of the same name.</summary>
    [Fact]
    public async Task Creating_a_system_role_evicts_everyone()
    {
        await Handler.Handle(new EntityCreated<SystemRoleEntity>(TestData.SystemRole()), Ct);

        Assert.True(_cache.InvalidatedAll);
    }

    [Fact]
    public async Task Changing_a_roles_permissions_evicts_everyone()
    {
        await Handler.Handle(new EntityUpdated<TeamRoleEntity>(TestData.TeamRole(), [nameof(TeamRoleEntity.Permissions)]), Ct);

        Assert.True(_cache.InvalidatedAll);
    }

    private sealed class RecordingCache : IPermissionClaimsCache
    {
        public List<Guid> Invalidated { get; } = [];
        public bool InvalidatedAll { get; set; }

        public Task<IReadOnlyList<Claim>> GetOrLoadAsync(Guid userId, string fingerprint, Func<Task<PermissionClaimsLoadResult>> load, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public void Invalidate(Guid userId) => Invalidated.Add(userId);

        public void InvalidateAll() => InvalidatedAll = true;
    }
}
