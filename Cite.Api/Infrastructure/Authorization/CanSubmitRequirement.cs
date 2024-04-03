// Copyright 2022 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license, please see LICENSE.md in the project root for license information or contact permission@sei.cmu.edu for full terms.

using Microsoft.AspNetCore.Authorization;
using System;
using System.Linq;
using System.Threading.Tasks;
using Cite.Api.Data;

namespace Cite.Api.Infrastructure.Authorization
{
    public class CanSubmitRequirement : IAuthorizationRequirement
    {
        public readonly Guid EvaluationId;
        public readonly CiteContext DbContext;
        
        public CanSubmitRequirement(Guid evaluationId, CiteContext dbContext)
        {
            EvaluationId = evaluationId;
            DbContext = dbContext;
        }
    }

    public class CanSubmitHandler : AuthorizationHandler<CanSubmitRequirement>, IAuthorizationHandler
    {
        protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, CanSubmitRequirement requirement)
        {
            var userId = context.User.Identities.First().Claims.First(c => c.Type == "sub")?.Value;
            var canSubmit = requirement.DbContext.TeamUsers
                .Any(tu => tu.Team.EvaluationId == requirement.EvaluationId &&
                    tu.UserId.ToString() == userId &&
                    tu.CanSubmit);
            if (canSubmit)
            {
                context.Succeed(requirement);
            }

            return Task.CompletedTask;
        }
    }
}

