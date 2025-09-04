// Copyright 2024 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.

using Cite.Api.Data;
using Microsoft.AspNetCore.Authorization;
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Cite.Api.Infrastructure.Authorization
{
    public class EvaluationPermissionRequirement : IAuthorizationRequirement
    {
        public EvaluationPermission[] RequiredPermissions;
        public Guid EvaluationId;

        public EvaluationPermissionRequirement(
            EvaluationPermission[] requiredPermissions,
            Guid projectId)
        {
            RequiredPermissions = requiredPermissions;
            EvaluationId = projectId;
        }
    }

    public class EvaluationPermissionHandler : AuthorizationHandler<EvaluationPermissionRequirement>, IAuthorizationHandler
    {
        protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, EvaluationPermissionRequirement requirement)
        {
            if (context.User == null)
            {
                context.Fail();
            }
            else
            {
                EvaluationPermissionClaim evaluationPermissionsClaim = null;

                var claims = context.User.Claims
                    .Where(x => x.Type == AuthorizationConstants.EvaluationPermissionClaimType)
                    .ToList();

                foreach (var claim in claims)
                {
                    var claimValue = EvaluationPermissionClaim.FromString(claim.Value);
                    if (claimValue.EvaluationId == requirement.EvaluationId)
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
