// Copyright 2022 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license, please see LICENSE.md in the project root for license information or contact permission@sei.cmu.edu for full terms.

using Microsoft.AspNetCore.Authorization;
using System;
using System.Linq;
using System.Threading.Tasks;
using Cite.Api.Data;

namespace Cite.Api.Infrastructure.Authorization
{
    public class TeamUserRequirement : IAuthorizationRequirement
    {
        public readonly Guid TeamId;
        public readonly CiteContext DbContext;

        public TeamUserRequirement(Guid teamId, CiteContext dbContext)
        {
            TeamId = teamId;
            DbContext = dbContext;
        }
    }

    public class TeamUserHandler : AuthorizationHandler<TeamUserRequirement>, IAuthorizationHandler
    {
        protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, TeamUserRequirement requirement)
        {
            var userId = context.User.Identities.First().Claims.First(c => c.Type == "sub")?.Value;
            var isTeamUser = requirement.DbContext.TeamUsers
                .Any(tu => tu.TeamId == requirement.TeamId && tu.UserId.ToString() == userId);
            if (isTeamUser)
            {
                context.Succeed(requirement);
            }

            return Task.CompletedTask;
        }
    }
}

