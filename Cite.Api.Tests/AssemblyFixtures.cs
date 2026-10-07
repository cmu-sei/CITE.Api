// Copyright 2026 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.

// cite.api uses the run-wide factory (CiteAppFactory): no test asserts on an NSubstitute double the host
// resolves, so every collaborator that leaves the process is a recorder or a stub.

using Cite.Api.Tests.Support;

// Starting a PostgreSQL container and running the migrations costs seconds, so it happens once for the
// whole assembly. xUnit v3 constructs this before the first test and injects it into any test class
// with a matching constructor parameter. The container itself starts on the first test that asks for a
// database (see PostgresTestDatabase), so tests that need none run without Docker.
[assembly: AssemblyFixture(typeof(DatabaseFixture))]

// Starting the application costs about a second, and everything it registers as a singleton is shared
// by every test that uses it: see CiteAppFactory, which says how each shared surface is dealt with.
[assembly: AssemblyFixture(typeof(CiteAppFactory))]
