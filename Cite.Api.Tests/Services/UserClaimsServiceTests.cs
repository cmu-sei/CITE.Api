// Copyright 2026 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.

using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.IdentityModel.JsonWebTokens;
using Cite.Api.Data.Enumerations;
using Cite.Api.Infrastructure.Authorization;
using Cite.Api.Infrastructure.Options;
using Cite.Api.Services;
using Cite.Api.Tests.Support;

namespace Cite.Api.Tests.Services;

/// <summary>
/// <see cref="UserClaimsService"/>, driven directly: the token paths (roles and groups from the identity
/// provider) and the cache that <see cref="TestConfiguration"/> turns off for the hosted application.
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
        using var cache = new MemoryCache(new MemoryCacheOptions());
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
        using var cache = new MemoryCache(new MemoryCacheOptions());
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

    private readonly MemoryCache _cache = new(new MemoryCacheOptions());

    private UserClaimsService Service(Cite.Api.Data.CiteContext context, ClaimsTransformationOptions options) =>
        new(context, _cache, options);

    public override async ValueTask DisposeAsync()
    {
        _cache.Dispose();
        await base.DisposeAsync();
    }

    private static ClaimsPrincipal Principal(Guid userId, params Claim[] claims) =>
        new(new ClaimsIdentity([new Claim("sub", userId.ToString()), .. claims], "Test"));
}
