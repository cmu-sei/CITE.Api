// Copyright 2026 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Cite.Api.Data;
using Cite.Api.Data.Enumerations;
using Cite.Api.Data.Models;
using Cite.Api.Infrastructure.Extensions;
using Cite.Api.Infrastructure.Options;
using Cite.Api.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Cite.Api.Tests.Infrastructure.Extensions;

/// <summary>
/// The stock scoring models in <c>Resources/ScoringModels</c>: one file per published standard, seeded at start-up.
/// </summary>
public class StockScoringModelsTests(DatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    /// <summary>The files as built: copied to the output directory by the Cite.Api project reference.</summary>
    private static string Directory_ => Path.Combine(AppContext.BaseDirectory, "Resources", "ScoringModels");

    private static readonly JsonSerializerOptions Json = new() { ReferenceHandler = ReferenceHandler.Preserve };

    private static IEnumerable<(string File, ScoringModelEntity Model)> Files() =>
        Directory.GetFiles(Directory_, "*.json").OrderBy(f => f, StringComparer.Ordinal)
            .Select(f => (Path.GetFileName(f), JsonSerializer.Deserialize<ScoringModelEntity>(File.ReadAllText(f), Json)!));

    /// <summary>
    /// Every file is a complete model in the import format, with fixed, unique ids down to its options, and a name of
    /// its own. Fixed ids are what make the seeding idempotent.
    /// </summary>
    [Fact]
    public void Every_stock_model_file_has_fixed_unique_ids_and_a_unique_name()
    {
        var files = Files().ToList();
        Assert.Equal(24, files.Count);

        var ids = new List<Guid>();
        foreach (var (file, model) in files)
        {
            Assert.False(string.IsNullOrWhiteSpace(model.Description), file);
            Assert.NotEqual(Guid.Empty, model.Id);
            ids.Add(model.Id);
            Assert.NotEmpty(model.ScoringCategories);
            foreach (var category in model.ScoringCategories)
            {
                Assert.NotEqual(Guid.Empty, category.Id);
                Assert.Equal(model.Id, category.ScoringModelId);
                ids.Add(category.Id);
                Assert.NotEmpty(category.ScoringOptions);
                foreach (var option in category.ScoringOptions)
                {
                    Assert.NotEqual(Guid.Empty, option.Id);
                    Assert.Equal(category.Id, option.ScoringCategoryId);
                    ids.Add(option.Id);
                }
            }
        }
        Assert.Equal(ids.Count, ids.Distinct().Count());
        Assert.Equal(files.Count, files.Select(f => f.Model.Description).Distinct().Count());
    }

    /// <summary>
    /// Seeding adds every file once. Running again adds nothing and changes nothing, which is what lets it run on
    /// every start-up; a stock model a planner has since edited is left as they left it.
    /// </summary>
    [Fact]
    public async Task SeedStockScoringModels_adds_every_file_once_and_keeps_edits()
    {
        await using var context = NewContext();

        var added = DatabaseExtensions.SeedStockScoringModels(context, Directory_);

        Assert.Equal(24, added);
        var stored = await ReadBack(db => db.ScoringModels.Include(m => m.ScoringCategories).ThenInclude(c => c.ScoringOptions)
            .Where(m => m.EvaluationId == null).ToListAsync(Ct));
        var fips = Assert.Single(stored, m => m.Description == "FIPS 199 Impact (Confidentiality, Integrity, Availability)");
        Assert.Equal(3, fips.ScoringCategories.Count);
        Assert.Equal(ItemStatus.Active, fips.Status);

        // a planner renames one; the next start-up must not put the file's name back
        await using (var edit = NewContext())
        {
            var model = await edit.ScoringModels.SingleAsync(m => m.Id == fips.Id, Ct);
            model.Description = "Our CIA model";
            await edit.SaveChangesAsync(Ct);
        }

        await using var again = NewContext();
        Assert.Equal(0, DatabaseExtensions.SeedStockScoringModels(again, Directory_));
        Assert.Equal(stored.Count, await ReadBack(db => db.ScoringModels.CountAsync(m => m.EvaluationId == null, Ct)));
        Assert.Equal("Our CIA model", await ReadBack(db => db.ScoringModels.Where(m => m.Id == fips.Id).Select(m => m.Description).SingleAsync(Ct)));
    }

    /// <summary>A missing directory seeds nothing rather than failing start-up.</summary>
    [Fact]
    public async Task SeedStockScoringModels_with_no_directory_adds_nothing()
    {
        await using var context = NewContext();

        Assert.Equal(0, DatabaseExtensions.SeedStockScoringModels(context, Path.Combine(Directory_, "absent")));
    }

    /// <summary>
    /// Start-up seeds the stock models by default and skips them when <c>Database:SeedStockScoringModels</c> is off.
    /// </summary>
    [Theory]
    [InlineData(true, 24)]
    [InlineData(false, 0)]
    public async Task InitializeDatabase_seeds_stock_models_unless_switched_off(bool seedStock, int expected)
    {
        using var host = new HostBuilder()
            .UseContentRoot(AppContext.BaseDirectory)
            .ConfigureAppConfiguration(builder => builder.AddInMemoryCollection(new Dictionary<string, string>()))
            .ConfigureServices(services =>
            {
                services.AddLogging();
                services.AddSingleton(new DatabaseOptions { Provider = "PostgreSQL", SeedFile = $"absent-{Guid.NewGuid()}.json", SeedStockScoringModels = seedStock });
                services.AddScoped<CiteContext>(_ => NewContext());
            })
            .Build();

        host.InitializeDatabase();

        Assert.Equal(expected, await ReadBack(db => db.ScoringModels.CountAsync(m => m.EvaluationId == null, Ct)));
    }
}
