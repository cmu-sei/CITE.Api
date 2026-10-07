// Copyright 2026 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.

using System.Net;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Cite.Api.Tests.Support;

namespace Cite.Api.Tests.Hubs;

/// <summary>
/// <c>MainHub</c>'s own <c>[Authorize(AuthenticationSchemes = "Bearer")]</c>, over a real SignalR connection to
/// the in-process server at <c>/hubs/main</c>, where <c>Startup.Configure</c> maps it.
/// </summary>
public class MainHubConnectionTests(DatabaseFixture fixture, CiteAppFactory factory) : ApiTestBase(fixture, factory)
{
    private const string HubPath = "/hubs/main";

    [Fact]
    public async Task An_actor_connects_and_joins()
    {
        var actor = await Actor().SeedAsync();
        await using var connection = Connection(actor);

        await connection.StartAsync(Ct);
        await connection.InvokeAsync("Join", Ct);

        Assert.Equal(HubConnectionState.Connected, connection.State);
    }

    /// <summary>A request with no identity is answered with a 401; the anonymous client is the case under test.</summary>
    [Fact]
    public async Task Negotiate_without_an_identity_is_unauthorized()
    {
        await AssertStatus(HttpStatusCode.Unauthorized, await Client().PostAsync($"{HubPath}/negotiate?negotiateVersion=1", null, Ct));
    }

    /// <summary>A WebSocket to the TestServer, carrying the headers every ApiTestBase client sends.</summary>
    private HubConnection Connection(TestActor actor)
    {
        var session = Client().DefaultRequestHeaders.GetValues(TestDatabaseScope.HeaderName).Single();

        return new HubConnectionBuilder()
            .WithUrl($"http://localhost{HubPath}", options =>
            {
                options.Transports = HttpTransportType.WebSockets;
                options.SkipNegotiation = true;
                options.WebSocketFactory = async (context, ct) =>
                {
                    var client = Factory.Server.CreateWebSocketClient();
                    client.ConfigureRequest = request =>
                    {
                        request.Headers[TestAuthHandler.UserHeader] = actor.Id.ToString();
                        request.Headers[TestAuthHandler.NameHeader] = actor.Name;
                        request.Headers[TestDatabaseScope.HeaderName] = session;
                    };

                    return await client.ConnectAsync(context.Uri, ct);
                };
            })
            .Build();
    }
}
