// Copyright 2026 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.

// cite.api: one "Permission" claim per system permission, and one JSON claim per evaluation, scoring model
// and team membership (EvaluationPermissionClaim, ScoringModelPermissionClaim, TeamPermissionClaim), the
// shapes UserClaimsService.GetPermissionClaims writes.

using System.Security.Claims;
using Cite.Api.Data.Enumerations;
using Cite.Api.Infrastructure.Authorization;

namespace Cite.Api.Tests.Support;

/// <summary>
/// Builds the principal the authorization stack sees for a signed-in user, for tests of the authorization
/// stack itself. An HTTP test's caller is a <see cref="TestActor"/>, never this.
/// </summary>
public sealed class ClaimsPrincipalBuilder
{
    private readonly List<Claim> _claims = [];
    private Guid _userId = Guid.NewGuid();
    private string _name = "Test User";

    public Guid UserId => _userId;

    public ClaimsPrincipalBuilder WithUserId(Guid userId)
    {
        _userId = userId;
        return this;
    }

    public ClaimsPrincipalBuilder WithName(string name)
    {
        _name = name;
        return this;
    }

    /// <summary>Adds system permissions, which grant across every resource.</summary>
    public ClaimsPrincipalBuilder WithSystemPermissions(params SystemPermission[] permissions)
    {
        foreach (var permission in permissions)
        {
            _claims.Add(new Claim(AuthorizationConstants.PermissionClaimType, permission.ToString()));
        }

        return this;
    }

    /// <summary>A raw system-permission value, for values that are not enum names.</summary>
    public ClaimsPrincipalBuilder WithRawSystemPermission(string value)
    {
        _claims.Add(new Claim(AuthorizationConstants.PermissionClaimType, value));
        return this;
    }

    /// <summary>The claim a membership on <paramref name="evaluationId"/> produces.</summary>
    public ClaimsPrincipalBuilder WithEvaluation(Guid evaluationId, params EvaluationPermission[] permissions)
    {
        _claims.Add(new Claim(
            AuthorizationConstants.EvaluationPermissionClaimType,
            new EvaluationPermissionClaim { EvaluationId = evaluationId, Permissions = permissions }.ToString()));
        return this;
    }

    /// <summary>The claim a membership on <paramref name="scoringModelId"/> produces.</summary>
    public ClaimsPrincipalBuilder WithScoringModel(Guid scoringModelId, params ScoringModelPermission[] permissions)
    {
        _claims.Add(new Claim(
            AuthorizationConstants.ScoringModelPermissionClaimType,
            new ScoringModelPermissionClaim { ScoringModelId = scoringModelId, Permissions = permissions }.ToString()));
        return this;
    }

    /// <summary>The claim a membership on <paramref name="teamId"/> produces.</summary>
    public ClaimsPrincipalBuilder WithTeam(Guid teamId, params TeamPermission[] permissions)
    {
        _claims.Add(new Claim(
            AuthorizationConstants.TeamPermissionClaimType,
            new TeamPermissionClaim { TeamId = teamId, Permissions = permissions }.ToString()));
        return this;
    }

    /// <summary>An arbitrary claim, for asserting that unrelated claim types are ignored.</summary>
    public ClaimsPrincipalBuilder WithClaim(string type, string value)
    {
        _claims.Add(new Claim(type, value));
        return this;
    }

    public ClaimsPrincipal Build()
    {
        var claims = new List<Claim>(_claims) { new("sub", _userId.ToString()), new("name", _name) };

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
    }

    /// <summary>An authenticated principal with no permissions, the baseline every check must reject.</summary>
    public static ClaimsPrincipal Anonymous() => new ClaimsPrincipalBuilder().Build();
}
