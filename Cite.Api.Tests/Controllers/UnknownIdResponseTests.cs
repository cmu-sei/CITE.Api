// Copyright 2026 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.

using System.Net;
using Cite.Api.Tests.Support;

namespace Cite.Api.Tests.Controllers;

/// <summary>
/// The GET-by-id actions that return their service's result without a null check: an id that names no row
/// reaches <c>Ok(null)</c>, which MVC's no-content formatter answers with an empty 204.
/// </summary>
public class UnknownIdResponseTests(DatabaseFixture fixture, CiteAppFactory factory) : ApiTestBase(fixture, factory)
{
    /// <summary>The route prefixes whose actions answer an unknown id this way; the test appends a fresh id.</summary>
    public static TheoryData<string> RoutesAnsweringNull =>
    [
        "api/groups/",
        "api/groups/memberships/",
        "api/system-roles/",
        "api/scoringOptions/",
    ];

    /// <summary>An unknown id is answered with an empty 204.</summary>
    [Theory]
    [MemberData(nameof(RoutesAnsweringNull))]
    public async Task Get_answers_an_unknown_id_with_no_content(string route)
    {
        var response = await RootClient.GetAsync(route + Guid.NewGuid(), Ct);

        await AssertStatus(HttpStatusCode.NoContent, response);
    }
}
