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
/// <c>ScoringCategoryController</c>: a scoring model's categories, read with ViewScoringModel (equations
/// included) or as an evaluation participant (equations masked), and changed with EditScoringModel.
/// </summary>
public class ScoringCategoryControllerTests(DatabaseFixture fixture, CiteAppFactory factory) : ApiTestBase(fixture, factory)
{
    /// <summary>A request with no identity is answered with a 401; the anonymous client is the case under test.</summary>
    [Fact]
    public async Task GetAll_without_an_identity_is_unauthorized()
    {
        await AssertStatus(HttpStatusCode.Unauthorized, await Client().GetAsync("api/scoringCategories", Ct));
    }

    [Fact]
    public async Task GetAll_lists_the_categories_matching_the_description_for_a_caller_holding_ViewScoringModels()
    {
        var (model, category) = await SeedTemplate("Findable Category");
        await Seed(TestData.ScoringCategory(model.Id, "Other Category", 2));
        var actor = await Actor().WithSystemPermissions(SystemPermission.ViewScoringModels).SeedAsync();

        var categories = await ReadAsync<List<ScoringCategory>>(await Client(actor).GetAsync("api/scoringCategories?description=Findable", Ct));

        Assert.Equal(category.Id, Assert.Single(categories).Id);
    }

    [Fact]
    public async Task GetAll_is_forbidden_for_a_caller_holding_ViewScoringModel_on_a_scoring_model()
    {
        var (model, _) = await SeedTemplate();
        var actor = await Actor().OnScoringModel(model.Id, permissions: [ScoringModelPermission.ViewScoringModel]).SeedAsync();

        await AssertApiError(HttpStatusCode.Forbidden, await Client(actor).GetAsync("api/scoringCategories", Ct));
    }

    [Fact]
    public async Task GetForScoringModel_lists_the_categories_with_their_equations_to_a_caller_holding_ViewScoringModel_on_it()
    {
        var (model, category) = await SeedTemplate();
        var actor = await Actor().OnScoringModel(model.Id, permissions: [ScoringModelPermission.ViewScoringModel]).SeedAsync();

        var categories = await ReadAsync<List<ScoringCategory>>(await Client(actor).GetAsync($"api/scoringModel/{model.Id}/scoringCategories", Ct));

        Assert.Equal(category.CalculationEquation, Assert.Single(categories).CalculationEquation);
    }

    [Fact]
    public async Task GetForScoringModel_masks_the_equations_of_an_evaluations_model_for_a_caller_holding_ObserveEvaluations()
    {
        var graph = await TestScenario.SeedEvaluationAsync(Db, Ct);
        var actor = await Actor().WithSystemPermissions(SystemPermission.ObserveEvaluations).SeedAsync();

        var categories = await ReadAsync<List<ScoringCategory>>(await Client(actor).GetAsync($"api/scoringModel/{graph.ScoringModel.Id}/scoringCategories", Ct));

        var category = Assert.Single(categories);
        Assert.Equal(("********", 0.0), (category.CalculationEquation, category.ScoringWeight));
    }

    [Fact]
    public async Task GetForScoringModel_lists_no_template_categories_to_a_caller_holding_ObserveEvaluations()
    {
        var (model, _) = await SeedTemplate();
        var actor = await Actor().WithSystemPermissions(SystemPermission.ObserveEvaluations).SeedAsync();

        var categories = await ReadAsync<List<ScoringCategory>>(await Client(actor).GetAsync($"api/scoringModel/{model.Id}/scoringCategories", Ct));

        Assert.Empty(categories);
    }

    /// <summary>A participant in the evaluation that uses the scoring model is answered with a 500.</summary>
    [Fact]
    public async Task GetForScoringModel_answers_a_caller_holding_ParticipateInEvaluation_with_a_server_error()
    {
        var graph = await TestScenario.SeedEvaluationAsync(Db, Ct);
        var actor = await Actor().OnEvaluation(graph.Evaluation.Id, permissions: [EvaluationPermission.ParticipateInEvaluation]).SeedAsync();

        var error = await AssertApiError(HttpStatusCode.InternalServerError, await Client(actor).GetAsync($"api/scoringModel/{graph.ScoringModel.Id}/scoringCategories", Ct));

        Assert.Equal("Handler for type ScoringModel is not implemented.", error.Detail);
    }

    // Same case as GetForScoringModel_answers_a_caller_holding_ParticipateInEvaluation_with_a_server_error.
    [Fact]
    public async Task GetForScoringModel_answers_a_caller_holding_only_ViewEvaluations_with_a_server_error()
    {
        var (model, _) = await SeedTemplate();
        var actor = await Actor().WithSystemPermissions(SystemPermission.ViewEvaluations).SeedAsync();

        var response = await Client(actor).GetAsync($"api/scoringModel/{model.Id}/scoringCategories", Ct);

        var error = await AssertApiError(HttpStatusCode.InternalServerError, response);
        Assert.Equal("Handler for type ScoringModel is not implemented.", error.Detail);
    }

    [Fact]
    public async Task Get_returns_the_category_to_a_caller_holding_ViewScoringModel_on_its_scoring_model()
    {
        var (model, category) = await SeedTemplate();
        var actor = await Actor().OnScoringModel(model.Id, permissions: [ScoringModelPermission.ViewScoringModel]).SeedAsync();

        var read = await ReadAsync<ScoringCategory>(await Client(actor).GetAsync($"api/scoringCategories/{category.Id}", Ct));

        Assert.Equal(category.Description, read.Description);
    }

    [Fact]
    public async Task Get_masks_the_equation_of_an_evaluations_category_for_a_caller_holding_ObserveEvaluations()
    {
        var graph = await TestScenario.SeedEvaluationAsync(Db, Ct);
        var actor = await Actor().WithSystemPermissions(SystemPermission.ObserveEvaluations).SeedAsync();

        var read = await ReadAsync<ScoringCategory>(await Client(actor).GetAsync($"api/scoringCategories/{graph.Category.Id}", Ct));

        Assert.Equal(("********", 0.0), (read.CalculationEquation, read.ScoringWeight));
    }

    [Fact]
    public async Task Get_is_forbidden_on_a_template_category_for_a_caller_holding_only_ObserveEvaluations()
    {
        var (_, category) = await SeedTemplate();
        var actor = await Actor().WithSystemPermissions(SystemPermission.ObserveEvaluations).SeedAsync();

        await AssertStatus(HttpStatusCode.Forbidden, await Client(actor).GetAsync($"api/scoringCategories/{category.Id}", Ct));
    }

    // Same case as GetForScoringModel_answers_a_caller_holding_ParticipateInEvaluation_with_a_server_error.
    [Fact]
    public async Task Get_answers_a_caller_holding_ParticipateInEvaluation_with_a_server_error()
    {
        var graph = await TestScenario.SeedEvaluationAsync(Db, Ct);
        var actor = await Actor().OnEvaluation(graph.Evaluation.Id, permissions: [EvaluationPermission.ParticipateInEvaluation]).SeedAsync();

        var error = await AssertApiError(HttpStatusCode.InternalServerError, await Client(actor).GetAsync($"api/scoringCategories/{graph.Category.Id}", Ct));
        Assert.Equal("Handler for type ScoringCategory is not implemented.", error.Detail);
    }

    /// <summary>An unknown category id is answered with a 500 for a caller the gate lets through.</summary>
    [Fact]
    public async Task Get_answers_an_unknown_category_with_a_server_error()
    {
        var error = await AssertApiError(HttpStatusCode.InternalServerError, await RootClient.GetAsync($"api/scoringCategories/{Guid.NewGuid()}", Ct));
        Assert.Equal("Object reference not set to an instance of an object.", error.Detail);
    }

    [Fact]
    public async Task Create_persists_the_category_and_broadcasts_its_scoring_model_for_a_caller_holding_EditScoringModel_on_it()
    {
        var (model, _) = await SeedTemplate();
        var actor = await Actor().OnScoringModel(model.Id, permissions: [ScoringModelPermission.EditScoringModel]).SeedAsync();
        var id = Guid.NewGuid();

        var response = await Client(actor).PostAsJsonAsync("api/scoringCategories", NewCategory(id, model.Id), Ct);

        await AssertStatus(HttpStatusCode.Created, response);
        await using var context = NewContext();
        Assert.Equal("New Category", (await context.ScoringCategories.SingleAsync(x => x.Id == id, Ct)).Description);
        var broadcast = Assert.Single(Factory.Hub<MainHub>().ToGroup(model.Id), x => x.Method == MainHubMethods.ScoringModelUpdated);
        Assert.Equal(model.Id, Assert.IsType<ScoringModel>(broadcast.Arguments[0]).Id);
    }

    [Fact]
    public async Task Create_is_forbidden_for_a_caller_holding_only_ViewScoringModel()
    {
        var (model, _) = await SeedTemplate();
        var actor = await Actor().OnScoringModel(model.Id, permissions: [ScoringModelPermission.ViewScoringModel]).SeedAsync();
        var id = Guid.NewGuid();

        await AssertStatus(HttpStatusCode.Forbidden, await Client(actor).PostAsJsonAsync("api/scoringCategories", NewCategory(id, model.Id), Ct));

        await using var context = NewContext();
        Assert.False(await context.ScoringCategories.AnyAsync(x => x.Id == id, Ct));
    }

    [Fact]
    public async Task Create_is_forbidden_for_a_caller_holding_EditScoringModel_only_on_another_scoring_model()
    {
        var (model, _) = await SeedTemplate();
        var actor = await Actor().OnNewScoringModel(ScoringModelPermission.EditScoringModel).SeedAsync();

        await AssertStatus(HttpStatusCode.Forbidden, await Client(actor).PostAsJsonAsync("api/scoringCategories", NewCategory(Guid.NewGuid(), model.Id), Ct));
    }

    [Fact]
    public async Task Update_changes_the_category_for_a_caller_holding_EditScoringModel_on_its_scoring_model()
    {
        var (model, category) = await SeedTemplate();
        var actor = await Actor().OnScoringModel(model.Id, permissions: [ScoringModelPermission.EditScoringModel]).SeedAsync();

        var response = await Client(actor).PutAsJsonAsync($"api/scoringCategories/{category.Id}", NewCategory(category.Id, model.Id) with { Description = "Changed" }, Ct);

        await AssertStatus(HttpStatusCode.OK, response);
        await using var context = NewContext();
        Assert.Equal("Changed", (await context.ScoringCategories.SingleAsync(x => x.Id == category.Id, Ct)).Description);
    }

    [Fact]
    public async Task Update_is_forbidden_for_a_caller_holding_only_ViewScoringModel()
    {
        var (model, category) = await SeedTemplate();
        var actor = await Actor().OnScoringModel(model.Id, permissions: [ScoringModelPermission.ViewScoringModel]).SeedAsync();

        await AssertStatus(HttpStatusCode.Forbidden, await Client(actor).PutAsJsonAsync($"api/scoringCategories/{category.Id}",
            NewCategory(category.Id, model.Id) with { Description = "Changed" }, Ct));

        await using var context = NewContext();
        Assert.Equal(category.Description, (await context.ScoringCategories.SingleAsync(x => x.Id == category.Id, Ct)).Description);
    }

    /// <summary>EditScoringModel on the scoring model named in the body lets a caller take another model's category.</summary>
    [Fact]
    public async Task Update_moves_another_models_category_into_the_model_the_caller_edits()
    {
        var (_, category) = await SeedTemplate();
        var actor = await Actor().OnNewScoringModel(ScoringModelPermission.EditScoringModel).SeedAsync();
        var editable = actor.NewScoringModelIds[0];

        var response = await Client(actor).PutAsJsonAsync($"api/scoringCategories/{category.Id}", NewCategory(category.Id, editable), Ct);

        await AssertStatus(HttpStatusCode.OK, response);
        await using var context = NewContext();
        Assert.Equal(editable, (await context.ScoringCategories.SingleAsync(x => x.Id == category.Id, Ct)).ScoringModelId);
    }

    [Fact]
    public async Task Delete_removes_the_category_and_its_options_for_a_caller_holding_EditScoringModel_on_its_scoring_model()
    {
        var (model, category) = await SeedTemplate();
        var actor = await Actor().OnScoringModel(model.Id, permissions: [ScoringModelPermission.EditScoringModel]).SeedAsync();

        await AssertStatus(HttpStatusCode.NoContent, await Client(actor).DeleteAsync($"api/scoringCategories/{category.Id}", Ct));

        await using var context = NewContext();
        Assert.False(await context.ScoringCategories.AnyAsync(x => x.Id == category.Id, Ct));
        Assert.False(await context.ScoringOptions.AnyAsync(x => x.ScoringCategoryId == category.Id, Ct));
    }

    [Fact]
    public async Task Delete_is_forbidden_for_a_caller_holding_EditScoringModel_only_on_another_scoring_model()
    {
        var (_, category) = await SeedTemplate();
        var actor = await Actor().OnNewScoringModel(ScoringModelPermission.EditScoringModel).SeedAsync();

        await AssertStatus(HttpStatusCode.Forbidden, await Client(actor).DeleteAsync($"api/scoringCategories/{category.Id}", Ct));

        await using var context = NewContext();
        Assert.True(await context.ScoringCategories.AnyAsync(x => x.Id == category.Id, Ct));
    }

    private async Task<(ScoringModelEntity Model, ScoringCategoryEntity Category)> SeedTemplate(string description = "Test Category")
    {
        var model = TestData.ScoringModel("Template");
        var category = TestData.ScoringCategory(model.Id, description);
        await Seed(model, category, TestData.ScoringOption(category.Id));

        return (model, category);
    }

    private static CategoryBody NewCategory(Guid id, Guid scoringModelId) =>
        new(id, 1, "New Category", "{sum}", 1.0, 0, 10, scoringModelId);

    private sealed record CategoryBody(
        Guid Id,
        int DisplayOrder,
        string Description,
        string CalculationEquation,
        double ScoringWeight,
        int MoveNumberFirstDisplay,
        int MoveNumberLastDisplay,
        Guid ScoringModelId);
}
