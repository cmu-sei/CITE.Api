// Copyright 2026 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.

using Cite.Api.Data;
using Cite.Api.Infrastructure.Extensions;
using Cite.Api.Infrastructure.Options;
using Cite.Api.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;

namespace Cite.Api.Tests.Infrastructure.Extensions;

public class DatabaseExtensionsTests(DatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    [Fact]
    public async Task InitializeDatabase_refuses_a_seed_assignment_with_an_existing_user_duty_pair_under_a_new_id()
    {
        var graph = await TestScenario.SeedEvaluationAsync(Db, Ct);
        var duty = TestData.Duty(graph.Evaluation.Id, graph.Team.Id);
        var user = TestData.User();
        var assignment = TestData.DutyUser(duty.Id, user.Id);
        await Seed(duty, user, assignment);
        var duplicateId = Guid.NewGuid();
        using var host = new HostBuilder()
            .ConfigureAppConfiguration(builder => builder.AddInMemoryCollection(new Dictionary<string, string>
            {
                ["SeedData:DutyUsers:0:Id"] = duplicateId.ToString(),
                ["SeedData:DutyUsers:0:DutyId"] = duty.Id.ToString(),
                ["SeedData:DutyUsers:0:UserId"] = user.Id.ToString()
            }))
            .ConfigureServices(services =>
            {
                services.AddLogging();
                services.AddSingleton(new DatabaseOptions { Provider = "PostgreSQL", SeedFile = $"absent-{duplicateId}.json" });
                services.AddScoped<CiteContext>(_ => NewContext());
            })
            .Build();

        var error = Assert.Throws<DbUpdateException>(() => host.InitializeDatabase());

        Assert.Equal("An error occurred while saving the entity changes. See the inner exception for details.", error.Message);
        var postgres = Assert.IsType<PostgresException>(error.InnerException);
        Assert.Equal(PostgresErrorCodes.UniqueViolation, postgres.SqlState);
        var stored = await ReadBack(db => db.DutyUsers.Where(x => x.DutyId == duty.Id && x.UserId == user.Id).ToListAsync(Ct));
        Assert.Equal(assignment.Id, Assert.Single(stored).Id);
    }
}
