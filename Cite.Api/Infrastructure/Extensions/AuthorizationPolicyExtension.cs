// Copyright 2022 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license, please see LICENSE.md in the project root for license information or contact permission@sei.cmu.edu for full terms.

using System;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Cite.Api.Infrastructure.Authorization;

namespace Cite.Api.Infrastructure.Extensions
{
    public static class AuthorizationPolicyExtensions
    {
        public static void AddAuthorizationPolicy(this IServiceCollection services, Options.AuthorizationOptions authOptions)
        {
            services.AddAuthorization(options =>
            {
                // Require all scopes in authOptions
                var policyBuilder = new AuthorizationPolicyBuilder().RequireAuthenticatedUser();
                Array.ForEach(authOptions.AuthorizationScope.Split(' '), x => policyBuilder.RequireClaim("scope", x));

                options.DefaultPolicy = policyBuilder.Build();
            });
            services.AddSingleton<IAuthorizationHandler, FullRightsHandler>();
            services.AddSingleton<IAuthorizationHandler, ContentDeveloperHandler>();
            services.AddSingleton<IAuthorizationHandler, OperatorHandler>();
            services.AddSingleton<IAuthorizationHandler, CanIncrementMoveHandler>();
            services.AddSingleton<IAuthorizationHandler, CanSubmitHandler>();
            services.AddSingleton<IAuthorizationHandler, CanModifyHandler>();
            services.AddSingleton<IAuthorizationHandler, BaseUserHandler>();
            services.AddSingleton<IAuthorizationHandler, EvaluationUserHandler>();
            services.AddSingleton<IAuthorizationHandler, TeamUserHandler>();
        }


    }
}
