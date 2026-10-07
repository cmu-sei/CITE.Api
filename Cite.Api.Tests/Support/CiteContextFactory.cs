// Copyright 2026 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.

// cite.api: CiteContext takes only its options. Production's UseConfiguredDatabase also sets split
// queries on the Npgsql provider, so a test context does too.

using Crucible.Common.EntityEvents.Interceptors;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Cite.Api.Data;

namespace Cite.Api.Tests.Support;

/// <summary>Builds <see cref="CiteContext"/> instances wired the way production wires them.</summary>
/// <remarks>
/// <see cref="CiteContext"/> extends <c>EventPublishingDbContext</c>, whose <c>PublishEventsAsync</c>
/// resolves <see cref="IMediator"/> and a logger off the settable <c>ServiceProvider</c> property with
/// <c>GetRequiredService</c>. Both must be registered or the first event-publishing save throws.
/// </remarks>
internal static class CiteContextFactory
{
    /// <summary>
    /// The provider a session shares across its contexts, and the substituted mediator tests assert on.
    /// A substitute is right here: each session gets its own, and only its own test reads it.
    /// </summary>
    public static (IServiceProvider Services, IMediator Mediator) CreateServices()
    {
        var mediator = Substitute.For<IMediator>();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(mediator);

        return (services.BuildServiceProvider(), mediator);
    }

    /// <summary>
    /// A context over the given provider configuration, with the entity event interceptor attached so
    /// SaveChanges publishes events exactly as it does in production.
    /// </summary>
    public static CiteContext CreateContext(
        Action<DbContextOptionsBuilder<CiteContext>> configureProvider,
        IServiceProvider services)
    {
        var builder = new DbContextOptionsBuilder<CiteContext>();
        configureProvider(builder);

        // DatabaseExtensions.UseConfiguredDatabase: production queries split, so Include chains a test
        // reads through behave as a request's do.
        builder.UseNpgsql(npgsql => npgsql.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery));
        builder.AddInterceptors(new EntityEventInterceptor(NullLogger<EntityEventInterceptor>.Instance));

        return new CiteContext(builder.Options) { ServiceProvider = services };
    }
}
