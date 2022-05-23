// Copyright 2022 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license, please see LICENSE.md in the project root for license information or contact permission@sei.cmu.edu for full terms.

using Microsoft.AspNetCore.Authorization;
using System;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Cite.Api.Infrastructure.Authorization
{
    public class OperatorRequirement : IAuthorizationRequirement
    {
        public OperatorRequirement()
        {
        }
    }

    public class OperatorHandler : AuthorizationHandler<OperatorRequirement>, IAuthorizationHandler
    {
        protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, OperatorRequirement requirement)
        {
            if (context.User.HasClaim(c => c.Type == CiteClaimTypes.SystemAdmin.ToString()) ||
                context.User.HasClaim(c => c.Type == CiteClaimTypes.ContentDeveloper.ToString()) ||
                context.User.HasClaim(c => c.Type == CiteClaimTypes.Operator.ToString()))
            {
                context.Succeed(requirement);
            }

            return Task.CompletedTask;
        }
    }
}

