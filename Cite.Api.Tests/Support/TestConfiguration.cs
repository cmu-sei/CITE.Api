// Copyright 2026 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.

namespace Cite.Api.Tests.Support;

/// <summary>
/// The configuration <see cref="CiteAppFactory"/> layers over the application's own <c>appsettings.json</c>.
/// </summary>
/// <remarks>
/// <c>WebApplicationFactory</c> resolves the content root to the <c>Cite.Api</c> project directory, so the
/// shipped configuration is already in force and only keys whose shipped value breaks or weakens a test run
/// belong here. Every entry states which. The provider and the host's connection string are host settings
/// in <see cref="CiteAppFactory"/>, because <c>Program.Main</c> reads them before this layer is added.
/// </remarks>
internal static class TestConfiguration
{
    public static Dictionary<string, string> Values => new()
    {
        // The shipped Sqlite would select a provider the harness never tests; requests reach PostgreSQL
        // through TestDatabaseScope, and the host's own InitializeDatabase migrates its throwaway clone.
        ["Database:Provider"] = "PostgreSQL",

        // The shipped true makes InitializeDatabase drop and recreate the host's database on every start.
        ["Database:DevModeRecreate"] = "false",

        // One host serves the whole run, and the claims cache is keyed on user id alone, so cached claims
        // would leak across tests: a user whose permissions one test seeds would keep them in the next
        // test that uses the same id. UserClaimsServiceTests drives the cache directly.
        ["ClaimsTransformation:EnableCaching"] = "false",
    };
}
