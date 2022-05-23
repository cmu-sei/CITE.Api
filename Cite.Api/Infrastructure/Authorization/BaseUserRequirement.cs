// Copyright 2022 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license, please see LICENSE.md in the project root for license information or contact permission@sei.cmu.edu for full terms.

using Microsoft.AspNetCore.Authorization;
using System;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Cite.Api.Infrastructure.Authorization
{
    public class BaseUserRequirement : IAuthorizationRequirement
    {
    }

    public class BaseUserHandler : AuthorizationHandler<BaseUserRequirement>, IAuthorizationHandler
    {
        protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, BaseUserRequirement requirement)
        {
            if (context.User.Identity.IsAuthenticated)
            {
                context.Succeed(requirement);
            }

            return Task.CompletedTask;
        }
    }
}

