// Copyright 2024 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.

using Cite.Api.Data.Enumerations;
using Microsoft.AspNetCore.Authorization;
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Cite.Api.Infrastructure.Authorization
{
    public class ScoringModelPermissionRequirement : IAuthorizationRequirement
    {
        public ScoringModelPermission[] RequiredPermissions;
        public Guid EvaluationId;

        public ScoringModelPermissionRequirement(
            ScoringModelPermission[] requiredPermissions,
            Guid projectId)
        {
            RequiredPermissions = requiredPermissions;
            EvaluationId = projectId;
        }
    }

    public class ScoringModelPermissionHandler : AuthorizationHandler<ScoringModelPermissionRequirement>, IAuthorizationHandler
    {
        protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, ScoringModelPermissionRequirement requirement)
        {
            if (context.User == null)
            {
                context.Fail();
            }
            else
            {
                ScoringModelPermissionClaim scoringModelPermissionClaim = null;

                var claims = context.User.Claims
                    .Where(x => x.Type == AuthorizationConstants.ScoringModelPermissionClaimType)
                    .ToList();

                foreach (var claim in claims)
                {
                    var claimValue = ScoringModelPermissionClaim.FromString(claim.Value);
                    if (claimValue.ScoringModelId == requirement.EvaluationId)
                    {
                        scoringModelPermissionClaim = claimValue;
                        break;
                    }
                }

                if (scoringModelPermissionClaim == null)
                {
                    context.Fail();
                }
                else if (requirement.RequiredPermissions == null || requirement.RequiredPermissions.Length == 0)
                {
                    context.Succeed(requirement);
                }
                else if (requirement.RequiredPermissions.Any(x => scoringModelPermissionClaim.Permissions.Contains(x)))
                {
                    context.Succeed(requirement);
                }
            }

            return Task.CompletedTask;
        }
    }
}
