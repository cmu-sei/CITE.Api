// Copyright 2026 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.

using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Crucible.Common.Authentication.Claims;
using Crucible.Common.EntityEvents.Events;
using Microsoft.Extensions.DependencyInjection;
using Cite.Api.Data.Models;
using Cite.Api.Infrastructure.EventHandlers;
using Microsoft.IdentityModel.JsonWebTokens;
using Cite.Api.Data.Enumerations;
using Cite.Api.Infrastructure.Authorization;
using Cite.Api.Infrastructure.Options;
using Cite.Api.Services;
using Cite.Api.Tests.Support;

namespace Cite.Api.Tests.Services;

/// <summary>
/// <see cref="UserClaimsService"/>, driven directly: the token paths (roles and groups from the identity
/// provider) and the shared <see cref="PermissionClaimsCache"/> that <see cref="TestConfiguration"/> turns off for
/// the hosted application.
/// </summary>
public class UserClaimsServiceTests(DatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    [Fact]
    public async Task AddUserClaims_with_update_creates_the_user_row_from_the_name_claim()
    {
        var userId = Guid.NewGuid();

        await using (var context = NewContext())
        {
            await Service(context, new()).AddUserClaims(Principal(userId, new Claim("name", "New Arrival")), true);
        }

        await using var read = NewContext();
        Assert.Equal("New Arrival", (await read.Users.SingleAsync(x => x.Id == userId, Ct)).Name);
    }

    [Fact]
    public async Task AddUserClaims_grants_a_system_role_named_in_the_tokens_realm_roles()
    {
        var user = TestData.User();
        await Seed(user);
        var options = new ClaimsTransformationOptions { UseRolesFromIdP = true, RolesClaimPath = "realm_access.roles" };
        var roles = new Claim("realm_access", """{"roles":["observer"]}""", JsonClaimValueTypes.Json);

        await using var context = NewContext();
        var principal = await Service(context, options).AddUserClaims(Principal(user.Id, roles), false);

        Assert.Contains(principal.Claims, x => x.Type == AuthorizationConstants.PermissionClaimType && x.Value == nameof(SystemPermission.ViewUsers));
        Assert.DoesNotContain(principal.Claims, x => x.Type == AuthorizationConstants.PermissionClaimType && x.Value == nameof(SystemPermission.ManageUsers));
    }

    [Fact]
    public async Task AddUserClaims_ignores_the_tokens_roles_while_UseRolesFromIdP_is_off()
    {
        var user = TestData.User();
        await Seed(user);
        var roles = new Claim("realm_access", """{"roles":["administrator"]}""", JsonClaimValueTypes.Json);

        await using var context = NewContext();
        var principal = await Service(context, new() { RolesClaimPath = "realm_access.roles" }).AddUserClaims(Principal(user.Id, roles), false);

        Assert.DoesNotContain(principal.Claims, x => x.Type == AuthorizationConstants.PermissionClaimType);
    }

    [Fact]
    public async Task AddUserClaims_grants_an_evaluation_membership_held_by_a_group_named_in_the_token()
    {
        var user = TestData.User();
        var scoringModel = TestData.ScoringModel();
        var evaluation = TestData.Evaluation(scoringModel.Id);
        var group = TestData.Group("Blue Cell");
        var role = TestData.EvaluationRole(EvaluationPermission.ObserveEvaluation);
        await Seed(user, scoringModel, evaluation, group, role, TestData.EvaluationMembership(evaluation.Id, null, role.Id, group.Id));
        var options = new ClaimsTransformationOptions { UseGroupsFromIdP = true, GroupsClaimPath = "groups" };

        await using var context = NewContext();
        var principal = await Service(context, options).AddUserClaims(Principal(user.Id, new Claim("groups", "blue cell")), false);

        var claim = EvaluationPermissionClaim.FromString(Assert.Single(principal.Claims, x => x.Type == AuthorizationConstants.EvaluationPermissionClaimType).Value);
        Assert.Equal(evaluation.Id, claim.EvaluationId);
    }

    [Fact]
    public async Task AddUserClaims_answers_from_the_cache_while_caching_is_on()
    {
        var user = TestData.User();
        var role = TestData.SystemRole(permissions: [SystemPermission.ViewUsers]);
        await Seed(role);
        user.RoleId = role.Id;
        await Seed(user);
        var cache = new PermissionClaimsCache();
        var options = new ClaimsTransformationOptions { EnableCaching = true, CacheExpirationSeconds = 60 };
        await using (var first = NewContext())
        {
            await new UserClaimsService(first, cache, options).AddUserClaims(Principal(user.Id), false);
        }
        await using (var change = NewContext())
        {
            (await change.Users.SingleAsync(x => x.Id == user.Id, Ct)).RoleId = null;
            await change.SaveChangesAsync(Ct);
        }

        await using var context = NewContext();
        var principal = await new UserClaimsService(context, cache, options).AddUserClaims(Principal(user.Id), false);

        Assert.Contains(principal.Claims, x => x.Type == AuthorizationConstants.PermissionClaimType && x.Value == nameof(SystemPermission.ViewUsers));
    }

    [Fact]
    public async Task RefreshClaims_drops_the_cached_claims()
    {
        var user = TestData.User();
        var role = TestData.SystemRole(permissions: [SystemPermission.ViewUsers]);
        await Seed(role);
        user.RoleId = role.Id;
        await Seed(user);
        var cache = new PermissionClaimsCache();
        var options = new ClaimsTransformationOptions { EnableCaching = true, CacheExpirationSeconds = 60 };
        await using (var first = NewContext())
        {
            var service = new UserClaimsService(first, cache, options);
            service.SetCurrentClaimsPrincipal(await service.AddUserClaims(Principal(user.Id), false));
        }
        await using (var change = NewContext())
        {
            (await change.Users.SingleAsync(x => x.Id == user.Id, Ct)).RoleId = null;
            await change.SaveChangesAsync(Ct);
        }

        await using var context = NewContext();
        var refreshing = new UserClaimsService(context, cache, options);
        refreshing.SetCurrentClaimsPrincipal(Principal(user.Id));
        var principal = await refreshing.RefreshClaims(user.Id);

        Assert.DoesNotContain(principal.Claims, x => x.Type == AuthorizationConstants.PermissionClaimType);
    }

    // ---- the shared cache

    /// <summary>
    /// Every silent renew and every browser tab brings a token with its own <c>jti</c>. The claims from the database
    /// depend on the roles and groups in the token, not on which token it is, so a second token reuses the entry: the
    /// role taken away in between is still granted, because nothing has invalidated the user. The old cache was
    /// dropped whenever the <c>jti</c> changed, which under load meant on almost every request.
    /// </summary>
    [Fact]
    public async Task A_second_token_with_its_own_jti_but_the_same_roles_and_groups_is_answered_from_the_cache()
    {
        var role = TestData.SystemRole(permissions: [SystemPermission.ViewUsers]);
        await Seed(role);
        var user = TestData.User(roleId: role.Id);
        await Seed(user);
        var cache = new CountingCache();
        await using (var first = NewContext())
        {
            await new UserClaimsService(first, cache, IdPOptions()).AddUserClaims(Token(user.Id, "token-1", ["observer"], ["blue cell"]), true);
        }
        await TakeTheRoleAway(user.Id);

        await using var context = NewContext();
        var principal = await new UserClaimsService(context, cache, IdPOptions()).AddUserClaims(Token(user.Id, "token-2", ["observer"], ["Blue Cell"]), true);

        Assert.Contains(principal.Claims, x => x.Type == AuthorizationConstants.PermissionClaimType && x.Value == nameof(SystemPermission.ViewUsers));
        Assert.Equal(1, cache.Loads);
    }

    /// <summary>
    /// The realm roles decide which system roles apply, so a token with other roles must not be handed the claims
    /// cached for the first one, in either direction.
    /// </summary>
    [Fact]
    public async Task A_token_with_different_realm_roles_is_loaded_on_its_own()
    {
        var user = TestData.User();
        await Seed(user);
        var cache = new CountingCache();

        await using (var first = NewContext())
        {
            var observer = await new UserClaimsService(first, cache, IdPOptions()).AddUserClaims(Token(user.Id, "token-1", ["observer"]), true);
            Assert.DoesNotContain(observer.Claims, x => x.Type == AuthorizationConstants.PermissionClaimType && x.Value == nameof(SystemPermission.ManageUsers));
        }

        await using var context = NewContext();
        var administrator = await new UserClaimsService(context, cache, IdPOptions()).AddUserClaims(Token(user.Id, "token-2", ["administrator"]), true);

        Assert.Contains(administrator.Claims, x => x.Type == AuthorizationConstants.PermissionClaimType && x.Value == nameof(SystemPermission.ManageUsers));
        Assert.Equal(2, cache.Loads);
    }

    /// <summary>
    /// A page load sends a user's requests together. They all miss together, and before the shared cache each one
    /// rebuilt the claims on a connection of its own.
    /// </summary>
    [Fact]
    public async Task Parallel_requests_from_one_user_load_from_the_database_once()
    {
        var (user, _) = await SeedEveryKindOfPermission();
        var cache = new CountingCache();
        const int requests = 20;
        var contexts = Enumerable.Range(0, requests).Select(_ => NewContext()).ToArray();

        try
        {
            var principals = await Task.WhenAll(contexts.Select((context, i) => Task.Run(() =>
                new UserClaimsService(context, cache, IdPOptions()).AddUserClaims(Token(user.Id, $"tab-{i}", ["observer"], ["blue cell"]), true), Ct)));

            Assert.Equal(1, cache.Loads);
            Assert.All(principals, principal => AssertEveryKindOfPermission(principal));
        }
        finally
        {
            foreach (var context in contexts)
            {
                await context.DisposeAsync();
            }
        }
    }

    /// <summary>
    /// System, evaluation (held by the user and by a token group), scoring model and team permissions all come out
    /// of the load, and the same again from the cache.
    /// </summary>
    [Fact]
    public async Task The_cached_claims_are_the_ones_the_database_grants()
    {
        var (user, _) = await SeedEveryKindOfPermission();
        var cache = new CountingCache();

        foreach (var jti in new[] { "token-1", "token-2" })
        {
            await using var context = NewContext();
            AssertEveryKindOfPermission(await new UserClaimsService(context, cache, IdPOptions()).AddUserClaims(Token(user.Id, jti, ["observer"], ["blue cell"]), true));
        }

        Assert.Equal(1, cache.Loads);
    }

    /// <summary>
    /// A membership saved after the claims were cached reaches the user's next request, through the entity event
    /// <see cref="PermissionClaimsCacheHandler"/> handles, without waiting for the entry to expire.
    /// </summary>
    [Fact]
    public async Task A_new_team_membership_takes_effect_on_the_next_request()
    {
        var user = TestData.User();
        var scoringModel = TestData.ScoringModel();
        var evaluation = TestData.Evaluation(scoringModel.Id);
        var teamType = TestData.TeamType();
        var team = TestData.Team(evaluation.Id, teamType.Id);
        var teamRole = TestData.TeamRole(TeamPermission.EditTeamScore);
        await Seed(user, scoringModel, evaluation, teamType, team, teamRole);
        var cache = new CountingCache();
        await using (var first = NewContext())
        {
            var before = await new UserClaimsService(first, cache, IdPOptions()).AddUserClaims(Token(user.Id, "token-1"), true);
            Assert.DoesNotContain(before.Claims, x => x.Type == AuthorizationConstants.TeamPermissionClaimType);
        }

        var membership = TestData.TeamMembership(team.Id, user.Id, teamRole.Id);
        await Seed(membership);
        await new PermissionClaimsCacheHandler(cache).Handle(new EntityCreated<TeamMembershipEntity>(membership), Ct);

        await using var context = NewContext();
        var after = await new UserClaimsService(context, cache, IdPOptions()).AddUserClaims(Token(user.Id, "token-1"), true);

        var claim = TeamPermissionClaim.FromString(Assert.Single(after.Claims, x => x.Type == AuthorizationConstants.TeamPermissionClaimType).Value);
        Assert.Equal(team.Id, claim.TeamId);
        Assert.Contains(TeamPermission.EditTeamScore, claim.Permissions);
    }

    /// <summary>
    /// The production wiring end to end: a team membership saved through <c>CiteContext</c> publishes its entity
    /// event through a real MediatR, <see cref="PermissionClaimsCacheHandler"/> evicts the user, and their next
    /// request (same token, caching on) sees the new team.
    /// </summary>
    [Fact]
    public async Task Saving_a_team_membership_through_the_context_evicts_the_cached_claims()
    {
        var user = TestData.User();
        var scoringModel = TestData.ScoringModel();
        var evaluation = TestData.Evaluation(scoringModel.Id);
        var teamType = TestData.TeamType();
        var team = TestData.Team(evaluation.Id, teamType.Id);
        var teamRole = TestData.TeamRole(TeamPermission.ViewTeam);
        await Seed(user, scoringModel, evaluation, teamType, team, teamRole);
        var cache = new CountingCache();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IPermissionClaimsCache>(cache);
        // only the cache handler: the SignalR and submission handlers need a host this test does not have
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<PermissionClaimsCacheHandler>();
            cfg.TypeEvaluator = type => type == typeof(PermissionClaimsCacheHandler);
        });
        await using var provider = services.BuildServiceProvider();

        await using (var first = NewContext())
        {
            var before = await new UserClaimsService(first, cache, IdPOptions()).AddUserClaims(Token(user.Id, "token-1"), true);
            Assert.DoesNotContain(before.Claims, x => x.Type == AuthorizationConstants.TeamPermissionClaimType);
        }

        await using (var save = Session.CreateContext(provider))
        {
            save.TeamMemberships.Add(TestData.TeamMembership(team.Id, user.Id, teamRole.Id));
            await save.SaveChangesAsync(Ct);
        }

        await using var context = NewContext();
        var after = await new UserClaimsService(context, cache, IdPOptions()).AddUserClaims(Token(user.Id, "token-1"), true);

        Assert.Equal(team.Id, TeamPermissionClaim.FromString(Assert.Single(after.Claims, x => x.Type == AuthorizationConstants.TeamPermissionClaimType).Value).TeamId);
        Assert.Equal(2, cache.Loads);
    }

    /// <summary>
    /// <see cref="UserClaimsService.RefreshClaims(ClaimsPrincipal)"/> (MainHub's) drops the entry the user's requests
    /// share, whatever token they came with.
    /// </summary>
    [Fact]
    public async Task RefreshClaims_for_a_principal_drops_the_entry_its_requests_share()
    {
        var role = TestData.SystemRole(permissions: [SystemPermission.ViewUsers]);
        await Seed(role);
        var user = TestData.User(roleId: role.Id);
        await Seed(user);
        var cache = new CountingCache();
        await using (var first = NewContext())
        {
            await new UserClaimsService(first, cache, IdPOptions()).AddUserClaims(Token(user.Id, "token-1", ["blue-team"]), true);
        }
        await TakeTheRoleAway(user.Id);

        await using (var hub = NewContext())
        {
            await new UserClaimsService(hub, cache, IdPOptions()).RefreshClaims(Token(user.Id, "token-1", ["blue-team"]));
        }

        await using var context = NewContext();
        var principal = await new UserClaimsService(context, cache, IdPOptions()).AddUserClaims(Token(user.Id, "token-2", ["blue-team"]), true);
        Assert.DoesNotContain(principal.Claims, x => x.Type == AuthorizationConstants.PermissionClaimType && x.Value == nameof(SystemPermission.ViewUsers));
    }

    /// <summary>
    /// The user row is written inside the cached load. A principal built without <c>update</c> for a user with no row
    /// yet is not kept, so the user's first real request still creates it.
    /// </summary>
    [Fact]
    public async Task A_user_with_no_row_is_not_cached_so_the_next_request_creates_it()
    {
        var userId = Guid.NewGuid();
        var cache = new CountingCache();
        await using (var first = NewContext())
        {
            await new UserClaimsService(first, cache, IdPOptions()).AddUserClaims(Token(userId, "token-1"), false);
        }

        await using (var context = NewContext())
        {
            await new UserClaimsService(context, cache, IdPOptions()).AddUserClaims(Token(userId, "token-2"), true);
        }

        Assert.Equal("Test User", (await ReadBack(db => db.Users.SingleAsync(x => x.Id == userId, Ct))).Name);
        Assert.Equal(2, cache.Loads);
    }

    /// <summary>The name is in the fingerprint, so a token carrying a new name misses and the load renames the row.</summary>
    [Fact]
    public async Task A_token_with_a_new_name_renames_the_user_despite_the_cache()
    {
        var user = TestData.User(name: "Old Name");
        await Seed(user);
        var cache = new CountingCache();
        await using (var first = NewContext())
        {
            await new UserClaimsService(first, cache, IdPOptions()).AddUserClaims(Token(user.Id, "token-1", name: "Old Name"), true);
        }

        await using (var context = NewContext())
        {
            await new UserClaimsService(context, cache, IdPOptions()).AddUserClaims(Token(user.Id, "token-2", name: "New Name"), true);
        }

        Assert.Equal("New Name", (await ReadBack(db => db.Users.SingleAsync(x => x.Id == user.Id, Ct))).Name);
    }

    private readonly PermissionClaimsCache _cache = new();

    private UserClaimsService Service(Cite.Api.Data.CiteContext context, ClaimsTransformationOptions options) =>
        new(context, _cache, options);

    private static ClaimsTransformationOptions IdPOptions() => new()
    {
        EnableCaching = true,
        CacheExpirationSeconds = 300,
        UseRolesFromIdP = true,
        RolesClaimPath = "realm_access.roles",
        UseGroupsFromIdP = true,
        GroupsClaimPath = "groups",
    };

    private static ClaimsPrincipal Principal(Guid userId, params Claim[] claims) =>
        new(new ClaimsIdentity([new Claim("sub", userId.ToString()), .. claims], "Test"));

    /// <summary>A token as Keycloak issues one: its own id, a name, realm roles and groups.</summary>
    private static ClaimsPrincipal Token(Guid userId, string jti, string[] roles = null, string[] groups = null, string name = "Test User") =>
        Principal(userId,
        [
            new Claim(JwtRegisteredClaimNames.Jti, jti),
            new Claim("name", name),
            new Claim("realm_access", System.Text.Json.JsonSerializer.Serialize(new { roles = roles ?? [] }), JsonClaimValueTypes.Json),
            .. (groups ?? []).Select(group => new Claim("groups", group)),
        ]);

    private async Task TakeTheRoleAway(Guid userId)
    {
        await using var change = NewContext();
        (await change.Users.SingleAsync(x => x.Id == userId, Ct)).RoleId = null;
        await change.SaveChangesAsync(Ct);
    }

    private sealed record Granted(Guid EvaluationId, Guid GroupEvaluationId, Guid ScoringModelId, Guid TeamId);

    private Granted _granted;

    private async Task<(UserEntity User, Granted Granted)> SeedEveryKindOfPermission()
    {
        var systemRole = TestData.SystemRole(permissions: [SystemPermission.ViewUsers]);
        await Seed(systemRole);
        var user = TestData.User(roleId: systemRole.Id);
        var scoringModel = TestData.ScoringModel();
        var evaluation = TestData.Evaluation(scoringModel.Id);
        var groupEvaluation = TestData.Evaluation(scoringModel.Id, "Group Evaluation");
        var group = TestData.Group("Blue Cell");
        var evaluationRole = TestData.EvaluationRole(EvaluationPermission.EditEvaluation);
        var groupEvaluationRole = TestData.EvaluationRole(EvaluationPermission.ObserveEvaluation);
        var scoringModelRole = TestData.ScoringModelRole(ScoringModelPermission.EditScoringModel);
        var teamType = TestData.TeamType();
        var team = TestData.Team(evaluation.Id, teamType.Id);
        var teamRole = TestData.TeamRole(TeamPermission.EditTeamScore);
        await Seed(user, scoringModel, evaluation, groupEvaluation, group, evaluationRole, groupEvaluationRole, scoringModelRole, teamType, team, teamRole,
            TestData.EvaluationMembership(evaluation.Id, user.Id, evaluationRole.Id),
            TestData.EvaluationMembership(groupEvaluation.Id, null, groupEvaluationRole.Id, group.Id),
            TestData.ScoringModelMembership(scoringModel.Id, user.Id, scoringModelRole.Id),
            TestData.TeamMembership(team.Id, user.Id, teamRole.Id));

        _granted = new Granted(evaluation.Id, groupEvaluation.Id, scoringModel.Id, team.Id);
        return (user, _granted);
    }

    private void AssertEveryKindOfPermission(ClaimsPrincipal principal)
    {
        Assert.Contains(principal.Claims, x => x.Type == AuthorizationConstants.PermissionClaimType && x.Value == nameof(SystemPermission.ViewUsers));
        // the realm role "observer" adds the seeded Observer system role's permissions
        Assert.DoesNotContain(principal.Claims, x => x.Type == AuthorizationConstants.PermissionClaimType && x.Value == nameof(SystemPermission.ManageUsers));

        var evaluations = principal.Claims
            .Where(x => x.Type == AuthorizationConstants.EvaluationPermissionClaimType)
            .Select(x => EvaluationPermissionClaim.FromString(x.Value))
            .ToDictionary(x => x.EvaluationId, x => x.Permissions);
        Assert.Contains(EvaluationPermission.EditEvaluation, evaluations[_granted.EvaluationId]);
        Assert.Contains(EvaluationPermission.ObserveEvaluation, evaluations[_granted.GroupEvaluationId]);

        var scoringModel = ScoringModelPermissionClaim.FromString(Assert.Single(principal.Claims, x => x.Type == AuthorizationConstants.ScoringModelPermissionClaimType).Value);
        Assert.Equal(_granted.ScoringModelId, scoringModel.ScoringModelId);
        Assert.Contains(ScoringModelPermission.EditScoringModel, scoringModel.Permissions);

        var team = TeamPermissionClaim.FromString(Assert.Single(principal.Claims, x => x.Type == AuthorizationConstants.TeamPermissionClaimType).Value);
        Assert.Equal(_granted.TeamId, team.TeamId);
        Assert.Contains(TeamPermission.EditTeamScore, team.Permissions);
    }

    /// <summary>The real cache, counting the loads it runs: each one is a round of permission queries.</summary>
    private sealed class CountingCache : IPermissionClaimsCache
    {
        private readonly PermissionClaimsCache _inner = new();
        private int _loads;

        public int Loads => Volatile.Read(ref _loads);

        public Task<IReadOnlyList<Claim>> GetOrLoadAsync(Guid userId, string fingerprint, Func<Task<PermissionClaimsLoadResult>> load, CancellationToken cancellationToken = default) =>
            _inner.GetOrLoadAsync(userId, fingerprint, () =>
            {
                Interlocked.Increment(ref _loads);
                return load();
            }, cancellationToken);

        public void Invalidate(Guid userId) => _inner.Invalidate(userId);

        public void InvalidateAll() => _inner.InvalidateAll();
    }
}
