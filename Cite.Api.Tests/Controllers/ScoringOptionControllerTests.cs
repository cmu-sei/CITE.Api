// Copyright 2026 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.

using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Cite.Api.Data.Enumerations;
using Cite.Api.Data.Models;
using Cite.Api.Hubs;
using Cite.Api.Tests.Support;
using Cite.Api.ViewModels;

namespace Cite.Api.Tests.Controllers;

/// <summary>
/// <c>ScoringOptionController</c>: a category's options, read with ViewScoringModel or as an evaluation
/// observer, and changed with EditScoringModel.
/// </summary>
public class ScoringOptionControllerTests(DatabaseFixture fixture, CiteAppFactory factory) : ApiTestBase(fixture, factory)
{
    /// <summary>A request with no identity is answered with a 401; the anonymous client is the case under test.</summary>
    [Fact]
    public async Task GetAll_without_an_identity_is_unauthorized()
    {
        await AssertStatus(HttpStatusCode.Unauthorized, await Client().GetAsync("api/scoringOptions", Ct));
    }

    [Fact]
    public async Task GetAll_lists_the_options_for_a_caller_holding_ViewScoringModels()
    {
        var (_, _, option) = await SeedTemplate();
        var actor = await Actor().WithSystemPermissions(SystemPermission.ViewScoringModels).SeedAsync();

        var options = await ReadAsync<List<ScoringOption>>(await Client(actor).GetAsync("api/scoringOptions", Ct));

        Assert.Contains(options, x => x.Id == option.Id);
    }

    [Fact]
    public async Task GetAll_is_forbidden_for_a_caller_holding_only_EditScoringModels()
    {
        var actor = await Actor().WithSystemPermissions(SystemPermission.EditScoringModels).SeedAsync();

        await AssertApiError(HttpStatusCode.Forbidden, await Client(actor).GetAsync("api/scoringOptions", Ct));
    }

    [Fact]
    public async Task GetForScoringCategory_lists_the_options_to_a_caller_holding_ViewScoringModel_on_the_scoring_model()
    {
        var (model, category, option) = await SeedTemplate();
        var actor = await Actor().OnScoringModel(model.Id, permissions: [ScoringModelPermission.ViewScoringModel]).SeedAsync();

        var options = await ReadAsync<List<ScoringOption>>(await Client(actor).GetAsync($"api/scoringCategory/{category.Id}/scoringOptions", Ct));

        Assert.Equal(option.Id, Assert.Single(options).Id);
    }

    [Fact]
    public async Task GetForScoringCategory_lists_the_options_to_a_caller_holding_ObserveEvaluations()
    {
        var graph = await TestScenario.SeedEvaluationAsync(Db, Ct);
        var actor = await Actor().WithSystemPermissions(SystemPermission.ObserveEvaluations).SeedAsync();

        var options = await ReadAsync<List<ScoringOption>>(await Client(actor).GetAsync($"api/scoringCategory/{graph.Category.Id}/scoringOptions", Ct));

        Assert.Equal(graph.Option.Id, Assert.Single(options).Id);
    }

    // Same case as ScoringCategoryControllerTests.GetForScoringModel_answers_a_caller_holding_ParticipateInEvaluation_with_a_server_error.
    [Fact]
    public async Task GetForScoringCategory_answers_a_caller_holding_ViewScoringModel_only_on_another_scoring_model_with_a_server_error()
    {
        var (_, category, _) = await SeedTemplate();
        var actor = await Actor().OnNewScoringModel(ScoringModelPermission.ViewScoringModel).SeedAsync();

        var error = await AssertApiError(HttpStatusCode.InternalServerError, await Client(actor).GetAsync($"api/scoringCategory/{category.Id}/scoringOptions", Ct));
        Assert.Equal("Handler for type ScoringCategory is not implemented.", error.Detail);
    }

    [Fact]
    public async Task Get_returns_the_option_to_a_caller_holding_ViewScoringModel_on_the_scoring_model()
    {
        var (model, _, option) = await SeedTemplate();
        var actor = await Actor().OnScoringModel(model.Id, permissions: [ScoringModelPermission.ViewScoringModel]).SeedAsync();

        var read = await ReadAsync<ScoringOption>(await Client(actor).GetAsync($"api/scoringOptions/{option.Id}", Ct));

        Assert.Equal(option.Value, read.Value);
    }

    // Same case as ScoringCategoryControllerTests.GetForScoringModel_answers_a_caller_holding_ParticipateInEvaluation_with_a_server_error.
    [Fact]
    public async Task Get_answers_a_caller_holding_ViewScoringModel_only_on_another_scoring_model_with_a_server_error()
    {
        var (_, _, option) = await SeedTemplate();
        var actor = await Actor().OnNewScoringModel(ScoringModelPermission.ViewScoringModel).SeedAsync();

        var error = await AssertApiError(HttpStatusCode.InternalServerError, await Client(actor).GetAsync($"api/scoringOptions/{option.Id}", Ct));
        Assert.Equal("Handler for type ScoringOption is not implemented.", error.Detail);
    }

    [Fact]
    public async Task Create_persists_the_option_and_broadcasts_its_scoring_model_for_a_caller_holding_EditScoringModels()
    {
        var (model, category, _) = await SeedTemplate();
        var actor = await Actor().WithSystemPermissions(SystemPermission.EditScoringModels).SeedAsync();
        var id = Guid.NewGuid();

        var response = await Client(actor).PostAsJsonAsync("api/scoringOptions", NewOption(id, category.Id), Ct);

        await AssertStatus(HttpStatusCode.Created, response);
        await using var context = NewContext();
        Assert.Equal("New Option", (await context.ScoringOptions.SingleAsync(x => x.Id == id, Ct)).Description);
        Assert.Single(Factory.Hub<MainHub>().ToGroup(model.Id), x => x.Method == MainHubMethods.ScoringModelUpdated);
    }

    /// <summary>EditScoringModel on the option's scoring model does not let its holder add an option there.</summary>
    [Fact]
    public async Task Create_is_forbidden_for_a_caller_holding_EditScoringModel_on_the_scoring_model()
    {
        var (model, category, _) = await SeedTemplate();
        var actor = await Actor().OnScoringModel(model.Id, permissions: [ScoringModelPermission.EditScoringModel]).SeedAsync();
        var id = Guid.NewGuid();

        await AssertStatus(HttpStatusCode.Forbidden, await Client(actor).PostAsJsonAsync("api/scoringOptions", NewOption(id, category.Id), Ct));

        await using var context = NewContext();
        Assert.False(await context.ScoringOptions.AnyAsync(x => x.Id == id, Ct));
    }

    [Fact]
    public async Task Create_is_forbidden_for_a_caller_holding_only_ViewScoringModels()
    {
        var (_, category, _) = await SeedTemplate();
        var actor = await Actor().WithSystemPermissions(SystemPermission.ViewScoringModels).SeedAsync();
        var id = Guid.NewGuid();

        await AssertStatus(HttpStatusCode.Forbidden, await Client(actor).PostAsJsonAsync("api/scoringOptions", NewOption(id, category.Id), Ct));

        await using var context = NewContext();
        Assert.False(await context.ScoringOptions.AnyAsync(x => x.Id == id, Ct));
    }

    [Fact]
    public async Task Update_changes_the_option_for_a_caller_holding_EditScoringModel_on_its_scoring_model()
    {
        var (model, category, option) = await SeedTemplate();
        var actor = await Actor().OnScoringModel(model.Id, permissions: [ScoringModelPermission.EditScoringModel]).SeedAsync();

        var response = await Client(actor).PutAsJsonAsync($"api/scoringOptions/{option.Id}", NewOption(option.Id, category.Id) with { Value = 5 }, Ct);

        await AssertStatus(HttpStatusCode.OK, response);
        await using var context = NewContext();
        Assert.Equal(5, (await context.ScoringOptions.SingleAsync(x => x.Id == option.Id, Ct)).Value);
    }

    [Fact]
    public async Task Update_is_forbidden_for_a_caller_holding_only_ViewScoringModel()
    {
        var (model, category, option) = await SeedTemplate();
        var actor = await Actor().OnScoringModel(model.Id, permissions: [ScoringModelPermission.ViewScoringModel]).SeedAsync();

        await AssertStatus(HttpStatusCode.Forbidden, await Client(actor).PutAsJsonAsync($"api/scoringOptions/{option.Id}", NewOption(option.Id, category.Id) with { Value = 5 }, Ct));

        await using var context = NewContext();
        Assert.Equal(option.Value, (await context.ScoringOptions.SingleAsync(x => x.Id == option.Id, Ct)).Value);
    }

    [Fact]
    public async Task Delete_removes_the_option_for_a_caller_holding_EditScoringModel_on_its_scoring_model()
    {
        var (model, _, option) = await SeedTemplate();
        var actor = await Actor().OnScoringModel(model.Id, permissions: [ScoringModelPermission.EditScoringModel]).SeedAsync();

        await AssertStatus(HttpStatusCode.NoContent, await Client(actor).DeleteAsync($"api/scoringOptions/{option.Id}", Ct));

        await using var context = NewContext();
        Assert.False(await context.ScoringOptions.AnyAsync(x => x.Id == option.Id, Ct));
    }

    [Fact]
    public async Task Delete_is_forbidden_for_a_caller_holding_EditScoringModel_only_on_another_scoring_model()
    {
        var (_, _, option) = await SeedTemplate();
        var actor = await Actor().OnNewScoringModel(ScoringModelPermission.EditScoringModel).SeedAsync();

        await AssertStatus(HttpStatusCode.Forbidden, await Client(actor).DeleteAsync($"api/scoringOptions/{option.Id}", Ct));

        await using var context = NewContext();
        Assert.True(await context.ScoringOptions.AnyAsync(x => x.Id == option.Id, Ct));
    }

    private async Task<(ScoringModelEntity Model, ScoringCategoryEntity Category, ScoringOptionEntity Option)> SeedTemplate()
    {
        var model = TestData.ScoringModel("Template");
        var category = TestData.ScoringCategory(model.Id);
        var option = TestData.ScoringOption(category.Id);
        await Seed(model, category, option);

        return (model, category, option);
    }

    private static OptionBody NewOption(Guid id, Guid scoringCategoryId) => new(id, 1, "New Option", false, 2, scoringCategoryId);

    private sealed record OptionBody(Guid Id, int DisplayOrder, string Description, bool IsModifier, double Value, Guid ScoringCategoryId);
}
