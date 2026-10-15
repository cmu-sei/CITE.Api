// Copyright 2026 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.

using System.Net;
using System.Text;
using Cite.Api.Tests.Support;
using GAC = Gallery.Api.Client;

namespace Cite.Api.Tests.Controllers;

/// <summary>
/// <c>GalleryController</c>: the caller's unread article count for an evaluation's Gallery exhibit, fetched
/// through the checked-in Gallery client with a resource-owner token. Every outbound request is answered by
/// <see cref="CiteAppFactory.OutboundHttp"/>.
/// </summary>
public class GalleryControllerTests(DatabaseFixture fixture, CiteAppFactory factory) : ApiTestBase(fixture, factory)
{
    /// <summary>The identity provider appsettings.json names for the resource-owner token.</summary>
    private const string Authority = "http://localhost:8080/realms/crucible";

    /// <summary>The Gallery API appsettings.json names (ClientSettings:GalleryApiUrl).</summary>
    private const string GalleryApiUrl = "http://localhost:4722/";

    /// <summary>A request with no identity is answered with a 401; the anonymous client is the case under test.</summary>
    [Fact]
    public async Task GetEvaluationUnreadArticleCount_without_an_identity_is_unauthorized()
    {
        await AssertStatus(HttpStatusCode.Unauthorized, await Client().GetAsync($"api/evaluations/{Guid.NewGuid()}/unreadarticlecount", Ct));
    }

    /// <summary>No permission is checked: an evaluation without an exhibit answers an empty count to any caller, and Gallery is not asked.</summary>
    [Fact]
    public async Task GetEvaluationUnreadArticleCount_answers_an_empty_count_for_an_evaluation_without_an_exhibit()
    {
        var graph = await TestScenario.SeedEvaluationAsync(Db, Ct);
        var actor = await Actor().SeedAsync();

        var unread = await ReadAsync<GAC.UnreadArticles>(await Client(actor).GetAsync($"api/evaluations/{graph.Evaluation.Id}/unreadarticlecount", Ct));

        Assert.Null(unread.Count);
        Assert.DoesNotContain(Factory.OutboundHttp.Requests, x => x.Contains(actor.Id.ToString()));
    }

    [Fact]
    public async Task GetEvaluationUnreadArticleCount_answers_an_unknown_evaluation_with_not_found()
    {
        await AssertApiError(HttpStatusCode.NotFound, await RootClient.GetAsync($"api/evaluations/{Guid.NewGuid()}/unreadarticlecount", Ct));
    }

    [Fact]
    public async Task GetEvaluationUnreadArticleCount_asks_gallery_for_the_callers_count_with_a_resource_owner_token()
    {
        var graph = await TestScenario.SeedEvaluationAsync(Db, Ct);
        var exhibitId = Guid.NewGuid();
        graph.Evaluation.GalleryExhibitId = exhibitId;
        await Db.SaveChangesAsync(Ct);
        var actor = await Actor().SeedAsync();
        var galleryUrl = $"{GalleryApiUrl}api/exhibits/{exhibitId}/users/{actor.Id}/articles/unread";
        StubIdentityProvider();
        Factory.OutboundHttp.Respond(galleryUrl,
            Json($$"""{"exhibitId":"{{exhibitId}}","userId":"{{actor.Id}}","count":"3"}"""), "application/json");

        var unread = await ReadAsync<GAC.UnreadArticles>(await Client(actor).GetAsync($"api/evaluations/{graph.Evaluation.Id}/unreadarticlecount", Ct));

        Assert.Equal("3", unread.Count);
        var sent = Assert.Single(Factory.OutboundHttp.Sent, x => x.Uri == galleryUrl);
        Assert.Equal("Bearer gallery-token", sent.Headers["Authorization"]);
    }

    private void StubIdentityProvider()
    {
        Factory.OutboundHttp
            .Respond($"{Authority}/.well-known/openid-configuration", Json($$"""
                {"issuer":"{{Authority}}","token_endpoint":"{{Authority}}/protocol/openid-connect/token","jwks_uri":"{{Authority}}/protocol/openid-connect/certs"}
                """), "application/json")
            .Respond($"{Authority}/protocol/openid-connect/certs", Json("""{"keys":[]}"""), "application/json")
            .Respond($"{Authority}/protocol/openid-connect/token",
                Json("""{"access_token":"gallery-token","token_type":"Bearer","expires_in":300}"""), "application/json");
    }

    private static byte[] Json(string json) => Encoding.UTF8.GetBytes(json);
}
