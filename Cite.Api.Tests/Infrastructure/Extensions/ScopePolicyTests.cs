// Copyright 2026 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.

using System.Net;
using Cite.Api.Data.Enumerations;
using Cite.Api.Tests.Support;

namespace Cite.Api.Tests.Infrastructure.Extensions;

/// <summary>
/// The <c>cite</c> scope (<c>Authorization:AuthorizationScope</c>) that the default policy
/// (<c>AuthorizationPolicyExtensions.AddAuthorizationPolicy</c>, through <c>BaseController</c>'s
/// <c>[Authorize]</c>) and the global MVC <c>AuthorizeFilter</c> (<c>Startup.ConfigureServices</c>) both
/// require of every controller request, whatever permissions the caller's rows grant.
/// </summary>
public class ScopePolicyTests(DatabaseFixture fixture, CiteAppFactory factory) : ApiTestBase(fixture, factory)
{
    /// <summary>A scope the token carries that is not the one the API requires.</summary>
    private const string OtherScope = "gallery";

    /// <summary>The scope the shipped <c>appsettings.json</c> requires.</summary>
    private const string CiteScope = "cite";

    [Fact]
    public async Task GetAll_is_forbidden_without_the_cite_scope_but_allowed_with_it()
    {
        var actor = await Actor().WithSystemPermissions(SystemPermission.ViewEvaluations).SeedAsync();

        using var deniedRequest = Scoped(OtherScope);
        using var controlRequest = Scoped(CiteScope);

        var denied = await Client(actor).SendAsync(deniedRequest, Ct);

        await AssertStatus(HttpStatusCode.Forbidden, denied);
        await AssertStatus(HttpStatusCode.OK, await Client(actor).SendAsync(controlRequest, Ct));
    }

    private static HttpRequestMessage Scoped(string scope)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "api/evaluations");
        request.Headers.Add(TestAuthHandler.ScopeHeader, scope);

        return request;
    }
}
