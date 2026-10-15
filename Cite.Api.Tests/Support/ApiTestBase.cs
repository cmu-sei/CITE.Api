// Copyright 2026 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.

// cite.api adds AssertApiError, the shared AssertJsonError<ApiError> plus its status assertion:
// JsonExceptionFilter answers a thrown exception with the app's ApiError as application/json. A request MVC
// cannot bind is answered first by [ApiController]'s automatic 400, a ProblemDetails, so AssertProblem
// applies there (MalformedRequestTests); the app's ValidateModelStateFilter never sees it.

using System.Net;
using Cite.Api.Data;
using Cite.Api.ViewModels;

namespace Cite.Api.Tests.Support;

/// <summary>
/// Base class for tests that drive the application over HTTP: the real routes, the real MVC filters, the
/// real claims transformer, the real handlers, over a database no other test can see.
/// </summary>
/// <remarks>
/// Derived classes forward both fixtures:
/// <c>MyTests(DatabaseFixture fixture, CiteAppFactory factory) : ApiTestBase(fixture, factory)</c>.
/// </remarks>
public abstract class ApiTestBase(DatabaseFixture fixture, CiteAppFactory factory)
    : ApiTestBase<CiteContext>(fixture, factory)
{
    protected DatabaseFixture Fixture { get; } = fixture;

    protected CiteAppFactory Factory { get; } = factory;

    /// <summary>
    /// An actor holding every system permission, for the tests that are about what an endpoint does
    /// rather than who may call it. Seeded before each test.
    /// </summary>
    protected TestActor Root { get; private set; } = null!;

    /// <summary>A client that acts as <see cref="Root"/>.</summary>
    protected HttpClient RootClient => Client(Root);

    /// <summary>Starts describing an actor to seed: <c>await Actor().WithSystemPermissions(...).SeedAsync()</c>.</summary>
    protected TestActorBuilder Actor() => new(Db, Ct);

    /// <summary>A client that acts as <paramref name="actor"/>, cached per actor.</summary>
    protected HttpClient Client(TestActor actor)
    {
        ArgumentNullException.ThrowIfNull(actor);

        return ClientFor(actor.Id, actor.Name);
    }

    /// <summary>
    /// The shared <see cref="ApiTestBase{TContext}.AssertJsonError{TError}"/> over cite.api's
    /// <see cref="ApiError"/>, plus the assertion on its <c>Status</c> member that the shared one leaves to
    /// the caller. A 500's <c>Detail</c> is the exception message, which says which failure was reached.
    /// </summary>
    protected static async Task<ApiError> AssertApiError(HttpStatusCode expected, HttpResponseMessage response)
    {
        var error = await AssertJsonError<ApiError>(expected, response);
        Assert.Equal((int)expected, error.Status);

        return error;
    }

    public override async ValueTask InitializeAsync()
    {
        await base.InitializeAsync();

        Root = await Actor().WithName("Root").WithAllSystemPermissions().SeedAsync();
    }
}
