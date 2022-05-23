// Copyright 2022 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license, please see LICENSE.md in the project root for license information or contact permission@sei.cmu.edu for full terms.

using Microsoft.AspNetCore.Authorization;
using System;
using System.Threading.Tasks;

namespace Cite.Api.Infrastructure.Authorization
{
    public class EvaluationUserRequirement : IAuthorizationRequirement
    {
        public readonly Guid EvaluationId;

        public EvaluationUserRequirement(Guid evaluationId)
        {
            EvaluationId = evaluationId;
        }
    }

    public class EvaluationUserHandler : AuthorizationHandler<EvaluationUserRequirement>, IAuthorizationHandler
    {
        protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, EvaluationUserRequirement requirement)
        {
            if (context.User.HasClaim(c => c.Type == CiteClaimTypes.SystemAdmin.ToString()) ||
                context.User.HasClaim(c => c.Type == CiteClaimTypes.ContentDeveloper.ToString()) ||
                (
                    context.User.HasClaim(c =>
                        c.Type == CiteClaimTypes.EvaluationUser.ToString() &&
                        c.Value.Contains(requirement.EvaluationId.ToString())
                    )
                )
            )
            {
                context.Succeed(requirement);
            }

            return Task.CompletedTask;
        }
    }
}

