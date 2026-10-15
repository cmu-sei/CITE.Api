// Copyright 2026 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.

// cite.api: CiteContext, migrations in Cite.Api.Migrations.PostgreSQL, and the run-wide factory's step 1B
// (Program.Main runs InitializeDatabase with no switch to skip it), so the host gets a database of its own.

using Cite.Api.Data;

namespace Cite.Api.Tests.Support;

/// <summary>
/// Owns the PostgreSQL database for the whole test run: starts it on first use and hands out an isolated
/// session per test.
/// </summary>
/// <remarks>
/// PostgreSQL exercises production's actual database, including the <c>if (Database.IsNpgsql())</c>
/// branch of <c>CiteContext.OnModelCreating</c> and the real migration history. A usable Docker daemon
/// is therefore required by every test that takes a database.
/// </remarks>
public sealed class DatabaseFixture : IAsyncLifetime, ITestDatabaseSessionSource<CiteContext>
{
    private static readonly PostgresTestDatabase<CiteContext> _database = new(new()
    {
        Name = "cite",
        TestAssembly = "Cite.Api.Tests",
        // Production computes this as {AssemblyName}.Migrations.{provider} in
        // DatabaseExtensions.UseConfiguredDatabase. Without it EF looks in the context's own assembly and
        // finds none.
        MigrationsAssembly = "Cite.Api.Migrations.PostgreSQL",
        CreateContext = CiteContextFactory.CreateContext,
        CreateServices = CiteContextFactory.CreateServices
    });

    /// <summary>
    /// The one database the host's own <c>Program.Main</c> migrates and seeds in <c>InitializeDatabase</c>.
    /// Lazy, and blocking only inside <c>ConfigureWebHost</c>, which runs when the first test uses the host,
    /// so tests that need no database still run without Docker. Never dropped: the container goes at the end.
    /// </summary>
    private static readonly Lazy<Task<ITestDatabaseSession<CiteContext>>> _host =
        new(() => _database.BeginSessionAsync());

    /// <summary>Nothing to do here: the container starts on the first request for a session.</summary>
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public Task<ITestDatabaseSession<CiteContext>> BeginSessionAsync() => _database.BeginSessionAsync();

    public ValueTask DisposeAsync() => _database.DisposeAsync();

    /// <summary>The host's throwaway database, cloned from the migrated template.</summary>
    public static ITestDatabaseSession<CiteContext> HostDatabase() => _host.Value.GetAwaiter().GetResult();
}
