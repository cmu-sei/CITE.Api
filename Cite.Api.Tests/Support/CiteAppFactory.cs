// Copyright 2026 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.

using System.Collections.Concurrent;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Cite.Api.Data;
using Cite.Api.Hubs;

namespace Cite.Api.Tests.Support;

/// <summary>
/// Hosts <c>Cite.Api</c> in process over <c>TestServer</c>, so that tests drive the real application: the
/// real <c>Startup</c>, the real MVC pipeline (<c>[ApiController]</c>'s automatic 400,
/// <c>JsonExceptionFilter</c>, the scope-requiring <c>AuthorizeFilter</c>), the real authorization stack and
/// the real claims transformer.
/// </summary>
/// <remarks>
/// <para>
/// One instance serves the whole run, declared in <c>AssemblyFixtures.cs</c>. Everything the application
/// registers as a singleton is therefore shared by every test, which is what
/// <see cref="TestConfiguration"/>'s claims-caching entry and <see cref="TestDatabaseScope"/> exist to
/// deal with.
/// </para>
/// <para>
/// <c>Program.CreateWebHostBuilder</c> matches neither convention <c>HostFactoryResolver</c> looks for, so
/// <c>WebApplicationFactory</c> invokes <c>Program.Main</c>, which runs <c>InitializeDatabase</c> with no
/// switch to skip it (step 1B). The host is therefore given a throwaway PostgreSQL database cloned from the
/// migrated template, so <c>Migrate()</c> is a no-op and the seeding runs against a database no test reads.
/// <c>Database:DevModeRecreate</c> is turned off in <see cref="TestConfiguration"/>, or the shipped
/// <c>true</c> would drop that database first.
/// </para>
/// <para>
/// Only three things are not the application's own: token validation (<see cref="TestAuthHandler"/>,
/// registered under the Bearer name the hub asks for), the
/// context registration (<see cref="TestDatabaseScope"/>), and the collaborators that leave the process:
/// the <see cref="MainHub"/> context (<see cref="Hub{THub}"/>), the Gallery and identity-provider calls
/// (<see cref="OutboundHttp"/>), and the xAPI background sender (removed with the other hosted services).
/// </para>
/// </remarks>
public sealed class CiteAppFactory : WebApplicationFactory<Program>, ITestHttpHost
{
    /// <summary>
    /// Answers every request the application makes over HTTP: the Gallery client and the resource-owner
    /// token requests in front of it. Arrange a url of your own on it.
    /// </summary>
    public StubHttpMessageHandler OutboundHttp { get; } = new();

    private readonly ConcurrentDictionary<Type, object> _hubs = new();

    /// <summary>What the application broadcast through a hub, per audience.</summary>
    public HubRecorder<THub> Hub<THub>() where THub : Hub =>
        (HubRecorder<THub>)_hubs.GetOrAdd(typeof(THub), _ => new HubRecorder<THub>());

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Production, so the developer exception page stays off and JsonExceptionFilter answers a 500 as it
        // does in a deployment: "A server error occurred." with the exception message as the detail.
        builder.UseEnvironment("Production");

        // 1B. No switch skips InitializeDatabase in Program.Main, so the host migrates and seeds a throwaway
        // database cloned from the template. Host settings reach Main as --key=value, ahead of the shipped
        // appsettings.json default of Sqlite.
        builder.UseSetting("Database:Provider", "PostgreSQL");
        builder.UseSetting("ConnectionStrings:PostgreSQL", DatabaseFixture.HostDatabase().ConnectionString);

        builder.ConfigureAppConfiguration(configuration =>
            configuration.AddInMemoryCollection(TestConfiguration.Values));

        builder.ConfigureTestServices(services =>
        {
            // XApiBackgroundService drains the xAPI queue against a database no test owns, so it does not run
            // in the hosted application.
            services.RemoveAll<IHostedService>();

            // MainHub names the Bearer scheme ([Authorize(AuthenticationSchemes = "Bearer")]), so the test
            // handler takes that name. Every registration that configures AuthenticationOptions is one
            // IConfigureOptions, so removing them drops Startup's JWT bearer claim on the name before the test
            // handler is registered under it; it is then both the default scheme and the hub's.
            services.RemoveAll<IConfigureOptions<AuthenticationOptions>>();
            services
                .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(JwtBearerDefaults.AuthenticationScheme, null);

            // InitializeDatabase resolves the context from a scope of its own, outside any request, where the
            // one-argument registration has no X-Test-Session header to route by and throws. Until the host
            // has started, such a resolution gets the host's own database, over that session's own services,
            // so the seed's entity events never reach the real handlers or the hub recorder; afterwards it
            // throws again, so a stray resolution outside a request still fails loudly.
            TestDatabaseScope.ReplaceRegistration<CiteContext>(
                services, () => _started ? null : DatabaseFixture.HostDatabase());

            // SignalR registers hub contexts as an open generic, which RemoveAll of a closed type cannot
            // match; a later closed registration wins on resolution.
            services.AddSingleton<IHubContext<MainHub>>(Hub<MainHub>());

            services.RemoveAll<IHttpClientFactory>();
            services.AddSingleton<IHttpClientFactory>(new StubHttpClientFactory(OutboundHttp));
        });
    }

    private readonly System.Threading.Lock _creating = new();
    private IHost _host;

    /// <summary>Set once the host has started, which is after <c>Program.Main</c>'s <c>InitializeDatabase</c>.</summary>
    private volatile bool _started;

    /// <summary>Builds the one host, under a lock, and hands it to every caller.</summary>
    /// <remarks>
    /// <c>WebApplicationFactory.StartServer</c> takes no lock, so two tests asking for their first client at
    /// once would each build a host (each running <c>Program.Main</c>), and the run would continue on two
    /// of them. Every way into the host (<c>CreateClient</c> in any overload, <c>Services</c>,
    /// <c>Server</c>) goes through <c>StartServer</c> to here, so this lock covers them all. A lock around
    /// <c>CreateClient()</c> alone would not: <c>Services</c>, <c>Server</c> and
    /// <c>CreateClient(options)</c> bypass it. <c>base.CreateHost</c> returns once the host has started,
    /// and the deferred host starts only after <c>Main</c> has run <c>InitializeDatabase</c>.
    /// </remarks>
    protected override IHost CreateHost(IHostBuilder builder)
    {
        lock (_creating)
        {
            if (_host is null)
            {
                _host = base.CreateHost(builder);
                _started = true;
            }

            return _host;
        }
    }
}
