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
/// Which saved changes drop whose cached permission claims. A change to one user's row or membership evicts that
/// user; a change that reaches users nobody can list (a group-held membership, a group's name, a role) evicts
/// everyone; anything else leaves the cache alone.
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
    public async Task Adding_changing_and_removing_a_team_membership_evict_its_user()
    {
        var membership = TestData.TeamMembership(Guid.NewGuid(), Guid.NewGuid());

        await Handler.Handle(new EntityCreated<TeamMembershipEntity>(membership), Ct);
        await Handler.Handle(new EntityUpdated<TeamMembershipEntity>(membership, [nameof(TeamMembershipEntity.RoleId)]), Ct);
        await Handler.Handle(new EntityDeleted<TeamMembershipEntity>(membership), Ct);

        Assert.Equal([membership.UserId, membership.UserId, membership.UserId], _cache.Invalidated);
        Assert.False(_cache.InvalidatedAll);
    }

    [Fact]
    public async Task A_group_membership_evicts_its_user()
    {
        var membership = TestData.GroupMembership(Guid.NewGuid(), Guid.NewGuid());

        await Handler.Handle(new EntityCreated<GroupMembershipEntity>(membership), Ct);

        Assert.Equal([membership.UserId], _cache.Invalidated);
    }

    [Fact]
    public async Task An_evaluation_membership_held_by_a_user_evicts_that_user()
    {
        var membership = TestData.EvaluationMembership(Guid.NewGuid(), Guid.NewGuid(), TestData.EvaluationRoles.Member);

        await Handler.Handle(new EntityCreated<EvaluationMembershipEntity>(membership), Ct);

        Assert.Equal([membership.UserId!.Value], _cache.Invalidated);
        Assert.False(_cache.InvalidatedAll);
    }

    /// <summary>A group's members include whoever the identity provider puts in a group of that name.</summary>
    [Fact]
    public async Task A_membership_held_by_a_group_evicts_everyone()
    {
        var evaluation = TestData.EvaluationMembership(Guid.NewGuid(), null, TestData.EvaluationRoles.Member, Guid.NewGuid());
        var scoringModel = TestData.ScoringModelMembership(Guid.NewGuid(), null, TestData.ScoringModelRoles.Editor, Guid.NewGuid());

        await Handler.Handle(new EntityDeleted<EvaluationMembershipEntity>(evaluation), Ct);
        Assert.True(_cache.InvalidatedAll);

        _cache.InvalidatedAll = false;
        await Handler.Handle(new EntityCreated<ScoringModelMembershipEntity>(scoringModel), Ct);
        Assert.True(_cache.InvalidatedAll);
    }

    /// <summary>The event carries only the new holder, so the old one cannot be singled out.</summary>
    [Fact]
    public async Task A_membership_moved_to_another_user_evicts_everyone()
    {
        var membership = TestData.ScoringModelMembership(Guid.NewGuid(), Guid.NewGuid(), TestData.ScoringModelRoles.Editor);

        await Handler.Handle(new EntityUpdated<ScoringModelMembershipEntity>(membership, [nameof(ScoringModelMembershipEntity.UserId)]), Ct);

        Assert.True(_cache.InvalidatedAll);
    }

    [Fact]
    public async Task Renaming_a_group_evicts_everyone_and_redescribing_it_nobody()
    {
        var group = TestData.Group();

        await Handler.Handle(new EntityUpdated<GroupEntity>(group, [nameof(GroupEntity.Description)]), Ct);
        Assert.False(_cache.InvalidatedAll);

        await Handler.Handle(new EntityUpdated<GroupEntity>(group, [nameof(GroupEntity.Name)]), Ct);
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
