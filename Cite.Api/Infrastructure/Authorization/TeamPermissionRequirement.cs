// Copyright 2025 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.

using Cite.Api.Data.Enumerations;
using Microsoft.AspNetCore.Authorization;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Cite.Api.Infrastructure.Authorization
{
    public class TeamPermissionRequirement : IAuthorizationRequirement
    {
        public TeamPermission[] RequiredPermissions;
        public Guid TeamId;

        public TeamPermissionRequirement(
            TeamPermission[] requiredPermissions,
            Guid projectId)
        {
            RequiredPermissions = requiredPermissions;
            TeamId = projectId;
        }
    }

    public class TeamPermissionHandler : AuthorizationHandler<TeamPermissionRequirement>, IAuthorizationHandler
    {
        protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, TeamPermissionRequirement requirement)
        {
            if (context.User == null)
            {
                context.Fail();
            }
            else
            {
                TeamPermissionClaim evaluationPermissionsClaim = null;

                var claims = context.User.Claims
                    .Where(x => x.Type == AuthorizationConstants.TeamPermissionClaimType)
                    .ToList();

                foreach (var claim in claims)
                {
                    var claimValue = TeamPermissionClaim.FromString(claim.Value);
                    if (claimValue.TeamId == requirement.TeamId)
                    {
                        evaluationPermissionsClaim = claimValue;
                        break;
                    }
                }

                if (evaluationPermissionsClaim == null)
                {
                    context.Fail();
                }
                else if (requirement.RequiredPermissions == null || requirement.RequiredPermissions.Length == 0)
                {
                    context.Succeed(requirement);
                }
                else if (requirement.RequiredPermissions.Any(x => evaluationPermissionsClaim.Permissions.Contains(x)))
                {
                    context.Succeed(requirement);
                }
            }

            return Task.CompletedTask;
        }
    }
}
