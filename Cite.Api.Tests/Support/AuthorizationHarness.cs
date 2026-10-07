// Copyright 2026 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.

// cite.api: AuthorizationPolicyExtensions.AddAuthorizationPolicy registers the four requirement handlers
// as singletons; ICiteAuthorizationService (AuthorizationService) asks them through IAuthorizationService.

using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Cite.Api.Infrastructure.Authorization;

namespace Cite.Api.Tests.Support;

/// <summary>The authorization stack wired as production wires it, for testing handlers directly.</summary>
public static class AuthorizationHarness
{
    /// <summary>The framework authorization service with the app's handlers registered.</summary>
    public static IAuthorizationService CreateFrameworkAuthorizationService()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorization();
        services.AddSingleton<IAuthorizationHandler, SystemPermissionHandler>();
        services.AddSingleton<IAuthorizationHandler, EvaluationPermissionHandler>();
        services.AddSingleton<IAuthorizationHandler, ScoringModelPermissionHandler>();
        services.AddSingleton<IAuthorizationHandler, TeamPermissionHandler>();

        return services.BuildServiceProvider().GetRequiredService<IAuthorizationService>();
    }

    /// <summary>
    /// Runs a requirement through a handler directly and returns the resulting context.
    /// </summary>
    public static async Task<AuthorizationHandlerContext> HandleAsync<TRequirement>(
        IAuthorizationHandler handler,
        TRequirement requirement,
        ClaimsPrincipal user,
        object resource = null)
        where TRequirement : IAuthorizationRequirement
    {
        var context = new AuthorizationHandlerContext([requirement], user, resource);
        await handler.HandleAsync(context);

        return context;
    }
}
