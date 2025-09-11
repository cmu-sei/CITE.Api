// Copyright 2024 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.

namespace Cite.Api.Infrastructure.Authorization;

public static class AuthorizationConstants
{
    public const string PermissionClaimType = "Permission";
    public const string EvaluationPermissionClaimType = "EvaluationPermission";
    public const string ScoringModelPermissionClaimType = "ScoringModelPermission";
    public const string TeamPermissionClaimType = "TeamPermission";
}
