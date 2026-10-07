// Copyright 2026 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.

using System.Net;
using System.Text;
using Microsoft.Extensions.Logging;
using Cite.Api.Infrastructure.Options;
using Cite.Api.Services;
using Cite.Api.Tests.Support;

namespace Cite.Api.Tests.Services;

/// <summary>
/// <see cref="GalleryService"/> driven directly over a <see cref="StubHttpMessageHandler"/> of its own, so the
/// identity provider and Gallery answer as each test chooses.
/// </summary>
public class GalleryServiceTests(DatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    /// <summary>A loopback authority, which IdentityModel's discovery policy accepts over http.</summary>
    private const string Authority = "http://localhost:9001/realms/gallery-service-tests";

    private const string GalleryApiUrl = "http://localhost:9002/";

    private readonly StubHttpMessageHandler _http = new();
    private readonly RecordingLogger<GalleryService> _logger = new();

    public override async ValueTask DisposeAsync()
    {
        _http.Dispose();
        await base.DisposeAsync();
    }

    /// <summary>An identity provider that does not answer discovery makes the request throw.</summary>
    [Fact]
    public async Task GetMyUnreadArticleCountAsync_throws_when_the_identity_provider_does_not_answer()
    {
        var evaluationId = await SeedEvaluationWithExhibit();

        await using var context = NewContext();
        await Assert.ThrowsAsync<Exception>(() => Service(context).GetMyUnreadArticleCountAsync(evaluationId, Ct));
    }

    [Fact]
    public async Task GetMyUnreadArticleCountAsync_logs_a_gallery_failure_and_answers_an_empty_count()
    {
        var evaluationId = await SeedEvaluationWithExhibit();
        StubIdentityProvider();

        await using var context = NewContext();
        var unread = await Service(context).GetMyUnreadArticleCountAsync(evaluationId, Ct);

        Assert.Null(unread.Count);
        Assert.Single(_logger.Entries, x => x.Level == LogLevel.Error);
    }

    private async Task<Guid> SeedEvaluationWithExhibit()
    {
        var graph = await TestScenario.SeedEvaluationAsync(Db, Ct);
        graph.Evaluation.GalleryExhibitId = Guid.NewGuid();
        await Db.SaveChangesAsync(Ct);

        return graph.Evaluation.Id;
    }

    private void StubIdentityProvider() =>
        _http
            .Respond($"{Authority}/.well-known/openid-configuration", Json($$"""
                {"issuer":"{{Authority}}","token_endpoint":"{{Authority}}/token","jwks_uri":"{{Authority}}/certs"}
                """), "application/json")
            .Respond($"{Authority}/certs", Json("""{"keys":[]}"""), "application/json")
            .Respond($"{Authority}/token", Json("""{"access_token":"t","token_type":"Bearer","expires_in":300}"""), "application/json");

    private GalleryService Service(Cite.Api.Data.CiteContext context) =>
        new(new StubHttpClientFactory(_http),
            new ClientOptions { GalleryApiUrl = GalleryApiUrl },
            new ClaimsPrincipalBuilder().Build(),
            context,
            AuthorizationHarness.CreateFrameworkAuthorizationService(),
            _logger,
            new ResourceOwnerAuthorizationOptions { Authority = Authority, ClientId = "cite-admin", UserName = "cite-admin", Scope = "gallery" });

    private static byte[] Json(string json) => Encoding.UTF8.GetBytes(json);
}
