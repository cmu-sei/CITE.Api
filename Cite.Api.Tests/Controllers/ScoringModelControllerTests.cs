// Copyright 2026 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Cite.Api.Data.Enumerations;
using Cite.Api.Data.Models;
using Cite.Api.Hubs;
using Cite.Api.Tests.Support;
using Cite.Api.ViewModels;

namespace Cite.Api.Tests.Controllers;

/// <summary>
/// <c>ScoringModelController</c>: each gate is a system permission or the matching scoring model permission
/// on the scoring model in the route; a scoring model copied for an evaluation is also readable, redacted, by
/// the members of that evaluation's teams.
/// </summary>
public class ScoringModelControllerTests(DatabaseFixture fixture, CiteAppFactory factory) : ApiTestBase(fixture, factory)
{
    /// <summary>A request with no identity is answered with a 401; the anonymous client is the case under test.</summary>
    [Fact]
    public async Task GetAll_without_an_identity_is_unauthorized()
    {
        await AssertStatus(HttpStatusCode.Unauthorized, await Client().GetAsync("api/scoringModels", Ct));
    }

    [Fact]
    public async Task GetAll_lists_the_unarchived_scoring_models_for_a_caller_holding_ViewScoringModels()
    {
        var active = TestData.ScoringModel("Active Model");
        var archived = TestData.ScoringModel("Archived Model");
        archived.Status = ItemStatus.Archived;
        await Seed(active, archived);
        var actor = await Actor().WithSystemPermissions(SystemPermission.ViewScoringModels).SeedAsync();

        var models = await ReadAsync<List<ScoringModel>>(await Client(actor).GetAsync("api/scoringModels", Ct));

        Assert.Contains(models, x => x.Id == active.Id);
        Assert.DoesNotContain(models, x => x.Id == archived.Id);
    }

    [Fact]
    public async Task GetAll_with_includeArchived_lists_the_archived_scoring_models_too()
    {
        var archived = TestData.ScoringModel("Archived Model");
        archived.Status = ItemStatus.Archived;
        await Seed(archived);

        var models = await ReadAsync<List<ScoringModel>>(await RootClient.GetAsync("api/scoringModels?includeArchived=true", Ct));

        Assert.Contains(models, x => x.Id == archived.Id);
    }

    [Fact]
    public async Task GetAll_lists_only_the_scoring_models_the_caller_created_for_a_caller_holding_ViewScoringModel_on_one()
    {
        var actorId = Guid.NewGuid();
        var created = TestData.ScoringModel("Mine");
        created.CreatedBy = actorId;
        var other = TestData.ScoringModel("Theirs");
        await Seed(created, other);
        var actor = await Actor().WithId(actorId).OnScoringModel(other.Id, permissions: [ScoringModelPermission.ViewScoringModel]).SeedAsync();

        var models = await ReadAsync<List<ScoringModel>>(await Client(actor).GetAsync("api/scoringModels", Ct));

        Assert.Equal([created.Id], models.Select(x => x.Id));
    }

    [Fact]
    public async Task Get_returns_the_template_with_its_equations_to_a_caller_holding_ViewScoringModel_on_it()
    {
        var (model, _) = await SeedTemplate();
        var actor = await Actor().OnScoringModel(model.Id, permissions: [ScoringModelPermission.ViewScoringModel]).SeedAsync();

        var read = await ReadAsync<ScoringModel>(await Client(actor).GetAsync($"api/scoringModels/{model.Id}", Ct));

        Assert.Equal("{sum}", Assert.Single(read.ScoringCategories).CalculationEquation);
        Assert.Equal([ScoringModelPermission.ViewScoringModel], read.ScoringModelPermissions);
    }

    [Fact]
    public async Task Get_returns_the_template_to_a_caller_holding_ViewScoringModels()
    {
        var (model, _) = await SeedTemplate();
        var actor = await Actor().WithSystemPermissions(SystemPermission.ViewScoringModels).SeedAsync();

        await AssertStatus(HttpStatusCode.OK, await Client(actor).GetAsync($"api/scoringModels/{model.Id}", Ct));
    }

    [Fact]
    public async Task Get_is_forbidden_for_a_caller_holding_ViewScoringModel_only_on_another_scoring_model()
    {
        var (model, _) = await SeedTemplate();
        var actor = await Actor().OnNewScoringModel(ScoringModelPermission.ViewScoringModel).SeedAsync();

        await AssertApiError(HttpStatusCode.Forbidden, await Client(actor).GetAsync($"api/scoringModels/{model.Id}", Ct));
    }

    [Fact]
    public async Task Get_is_forbidden_on_a_template_for_a_caller_holding_only_ObserveEvaluations()
    {
        var (model, _) = await SeedTemplate();
        var actor = await Actor().WithSystemPermissions(SystemPermission.ObserveEvaluations).SeedAsync();

        await AssertStatus(HttpStatusCode.Forbidden, await Client(actor).GetAsync($"api/scoringModels/{model.Id}", Ct));
    }

    [Fact]
    public async Task Get_returns_an_evaluations_scoring_model_with_redacted_equations_to_a_member_of_one_of_its_teams()
    {
        var graph = await TestScenario.SeedEvaluationAsync(Db, Ct);
        var actor = await Actor().OnTeam(graph.Team.Id).SeedAsync();

        var read = await ReadAsync<ScoringModel>(await Client(actor).GetAsync($"api/scoringModels/{graph.ScoringModel.Id}", Ct));

        var category = Assert.Single(read.ScoringCategories);
        Assert.Equal(("Redacted", 0.0), (category.CalculationEquation, category.ScoringWeight));
    }

    [Fact]
    public async Task Get_is_forbidden_on_an_evaluations_scoring_model_for_a_member_of_a_team_in_another_evaluation()
    {
        var graph = await TestScenario.SeedEvaluationAsync(Db, Ct);
        var other = await TestScenario.SeedEvaluationAsync(Db, Ct, "Other");
        var actor = await Actor().OnNewTeam(other.Evaluation.Id, TeamPermission.ViewTeam).SeedAsync();

        await AssertStatus(HttpStatusCode.Forbidden, await Client(actor).GetAsync($"api/scoringModels/{graph.ScoringModel.Id}", Ct));
    }

    /// <summary>An unknown scoring model id is answered with a 500.</summary>
    [Fact]
    public async Task Get_answers_an_unknown_scoring_model_with_a_server_error()
    {
        var error = await AssertApiError(HttpStatusCode.InternalServerError, await RootClient.GetAsync($"api/scoringModels/{Guid.NewGuid()}", Ct));
        Assert.Equal("Object reference not set to an instance of an object.", error.Detail);
    }

    [Fact]
    public async Task Create_persists_the_scoring_model_for_a_caller_holding_CreateScoringModels()
    {
        var actor = await Actor().WithSystemPermissions(SystemPermission.CreateScoringModels).SeedAsync();

        var response = await Client(actor).PostAsJsonAsync("api/scoringModels",
            new { description = "Created Model", status = "Active", calculationEquation = "{sum}", useSubmit = true }, Ct);

        var created = await ReadAsync<ScoringModel>(response);
        await using var context = NewContext();
        var stored = await context.ScoringModels.SingleAsync(x => x.Id == created.Id, Ct);
        Assert.True(stored.UseSubmit);
        Assert.Equal(actor.Id, stored.CreatedBy);
    }

    [Fact]
    public async Task Create_broadcasts_the_new_scoring_model_to_its_group()
    {
        var id = Guid.NewGuid();

        await AssertStatus(HttpStatusCode.Created, await RootClient.PostAsJsonAsync("api/scoringModels",
            new { id, description = "Broadcast Model", status = "Active" }, Ct));

        var broadcast = Assert.Single(Factory.Hub<MainHub>().ToGroup(id), x => x.Method == MainHubMethods.ScoringModelCreated);
        Assert.Equal("Broadcast Model", Assert.IsType<ScoringModel>(broadcast.Arguments[0]).Description);
    }

    [Fact]
    public async Task Create_is_forbidden_for_a_caller_holding_only_EditScoringModels()
    {
        var actor = await Actor().WithSystemPermissions(SystemPermission.EditScoringModels).SeedAsync();

        var response = await Client(actor).PostAsJsonAsync("api/scoringModels", new { description = "Refused Model", status = "Active" }, Ct);

        await AssertStatus(HttpStatusCode.Forbidden, response);
        await using var context = NewContext();
        Assert.False(await context.ScoringModels.AnyAsync(x => x.Description == "Refused Model", Ct));
    }

    [Fact]
    public async Task Copy_copies_categories_and_options_for_a_caller_holding_CreateScoringModels_and_ViewScoringModel_on_it()
    {
        var (model, _) = await SeedTemplate();
        var actor = await Actor().WithName("Copier").WithSystemPermissions(SystemPermission.CreateScoringModels)
            .OnScoringModel(model.Id, permissions: [ScoringModelPermission.ViewScoringModel]).SeedAsync();

        var copy = await ReadAsync<ScoringModel>(await Client(actor).PostAsync($"api/scoringModels/{model.Id}/copy", null, Ct));

        Assert.Equal("Template *copy by Copier*", copy.Description);
        await using var context = NewContext();
        var stored = await context.ScoringModels.Include(x => x.ScoringCategories).ThenInclude(x => x.ScoringOptions).SingleAsync(x => x.Id == copy.Id, Ct);
        Assert.Single(Assert.Single(stored.ScoringCategories).ScoringOptions);
    }

    [Fact]
    public async Task Copy_is_forbidden_for_a_caller_holding_CreateScoringModels_and_ViewScoringModel_only_on_another_scoring_model()
    {
        var (model, _) = await SeedTemplate();
        var actor = await Actor().WithSystemPermissions(SystemPermission.CreateScoringModels).OnNewScoringModel(ScoringModelPermission.ViewScoringModel).SeedAsync();

        await AssertStatus(HttpStatusCode.Forbidden, await Client(actor).PostAsync($"api/scoringModels/{model.Id}/copy", null, Ct));
    }

    [Fact]
    public async Task Copy_is_forbidden_for_a_caller_holding_only_ViewScoringModels()
    {
        var (model, _) = await SeedTemplate();
        var actor = await Actor().WithSystemPermissions(SystemPermission.ViewScoringModels).SeedAsync();

        await AssertStatus(HttpStatusCode.Forbidden, await Client(actor).PostAsync($"api/scoringModels/{model.Id}/copy", null, Ct));

        await using var context = NewContext();
        Assert.False(await context.ScoringModels.AnyAsync(x => x.Id != model.Id && x.Description.StartsWith("Template"), Ct));
    }

    [Fact]
    public async Task Update_changes_the_scoring_model_for_a_caller_holding_EditScoringModel_on_it()
    {
        var (model, _) = await SeedTemplate();
        var actor = await Actor().OnScoringModel(model.Id, permissions: [ScoringModelPermission.EditScoringModel]).SeedAsync();

        var response = await Client(actor).PutAsJsonAsync($"api/scoringModels/{model.Id}", new { id = model.Id, description = "Renamed", status = "Active" }, Ct);

        await AssertStatus(HttpStatusCode.OK, response);
        await using var context = NewContext();
        Assert.Equal("Renamed", (await context.ScoringModels.SingleAsync(x => x.Id == model.Id, Ct)).Description);
    }

    [Fact]
    public async Task Update_is_forbidden_for_a_caller_holding_only_ViewScoringModel()
    {
        var (model, _) = await SeedTemplate();
        var actor = await Actor().OnScoringModel(model.Id, permissions: [ScoringModelPermission.ViewScoringModel]).SeedAsync();

        var response = await Client(actor).PutAsJsonAsync($"api/scoringModels/{model.Id}", new { id = model.Id, description = "Renamed", status = "Active" }, Ct);

        await AssertStatus(HttpStatusCode.Forbidden, response);
        await using var context = NewContext();
        Assert.Equal("Template", (await context.ScoringModels.SingleAsync(x => x.Id == model.Id, Ct)).Description);
    }

    [Fact]
    public async Task Update_is_forbidden_for_a_caller_holding_EditScoringModel_only_on_another_scoring_model()
    {
        var (model, _) = await SeedTemplate();
        var actor = await Actor().OnNewScoringModel(ScoringModelPermission.EditScoringModel).SeedAsync();

        await AssertStatus(HttpStatusCode.Forbidden, await Client(actor).PutAsJsonAsync($"api/scoringModels/{model.Id}",
            new { id = model.Id, description = "Renamed", status = "Active" }, Ct));
    }

    [Fact]
    public async Task Delete_removes_the_template_and_its_categories_for_a_caller_holding_ManageScoringModel_on_it()
    {
        var (model, category) = await SeedTemplate();
        var actor = await Actor().OnScoringModel(model.Id, permissions: [ScoringModelPermission.ManageScoringModel]).SeedAsync();

        await AssertStatus(HttpStatusCode.NoContent, await Client(actor).DeleteAsync($"api/scoringModels/{model.Id}", Ct));

        await using var context = NewContext();
        Assert.False(await context.ScoringModels.AnyAsync(x => x.Id == model.Id, Ct));
        Assert.False(await context.ScoringCategories.AnyAsync(x => x.Id == category.Id, Ct));
    }

    [Fact]
    public async Task Delete_is_forbidden_for_a_caller_holding_only_EditScoringModel()
    {
        var (model, _) = await SeedTemplate();
        var actor = await Actor().OnScoringModel(model.Id, permissions: [ScoringModelPermission.EditScoringModel]).SeedAsync();

        await AssertStatus(HttpStatusCode.Forbidden, await Client(actor).DeleteAsync($"api/scoringModels/{model.Id}", Ct));

        await using var context = NewContext();
        Assert.True(await context.ScoringModels.AnyAsync(x => x.Id == model.Id, Ct));
    }

    /// <summary>Deleting the scoring model an evaluation is scored with is answered with a 500, and both stay.</summary>
    [Fact]
    public async Task Delete_answers_a_scoring_model_an_evaluation_uses_with_a_server_error()
    {
        var graph = await TestScenario.SeedEvaluationAsync(Db, Ct);

        var error = await AssertApiError(HttpStatusCode.InternalServerError, await RootClient.DeleteAsync($"api/scoringModels/{graph.ScoringModel.Id}", Ct));
        Assert.StartsWith("An error occurred while saving the entity changes.", error.Detail);

        await using var context = NewContext();
        Assert.True(await context.ScoringModels.AnyAsync(x => x.Id == graph.ScoringModel.Id, Ct));
    }

    [Fact]
    public async Task DownloadJson_answers_the_model_with_its_categories_to_a_caller_holding_ViewScoringModel_on_it()
    {
        var (model, category) = await SeedTemplate();
        var actor = await Actor().OnScoringModel(model.Id, permissions: [ScoringModelPermission.ViewScoringModel]).SeedAsync();

        var response = await Client(actor).GetAsync($"api/scoringModels/{model.Id}/json", Ct);

        await AssertStatus(HttpStatusCode.OK, response);
        Assert.Equal("Template.json", response.Content.Headers.ContentDisposition?.FileName);
        Assert.Contains(category.Id.ToString(), await response.Content.ReadAsStringAsync(Ct));
    }

    [Fact]
    public async Task DownloadJson_is_forbidden_for_a_caller_holding_ViewScoringModel_only_on_another_scoring_model()
    {
        var (model, _) = await SeedTemplate();
        var actor = await Actor().OnNewScoringModel(ScoringModelPermission.ViewScoringModel).SeedAsync();

        await AssertStatus(HttpStatusCode.Forbidden, await Client(actor).GetAsync($"api/scoringModels/{model.Id}/json", Ct));
    }

    [Fact]
    public async Task UploadJson_imports_a_downloaded_model_as_a_copy_for_a_caller_holding_CreateScoringModels()
    {
        var (model, _) = await SeedTemplate();
        var exported = await (await RootClient.GetAsync($"api/scoringModels/{model.Id}/json", Ct)).Content.ReadAsByteArrayAsync(Ct);
        var actor = await Actor().WithName("Importer").WithSystemPermissions(SystemPermission.CreateScoringModels).SeedAsync();

        var imported = await ReadAsync<ScoringModel>(await Client(actor).PostAsync("api/scoringModels/json", Upload(exported), Ct));

        Assert.NotEqual(model.Id, imported.Id);
        Assert.Equal("Template *copy by Importer*", imported.Description);
        Assert.Single(imported.ScoringCategories);
    }

    [Fact]
    public async Task UploadJson_is_forbidden_for_a_caller_holding_only_EditScoringModels()
    {
        var (model, _) = await SeedTemplate();
        var exported = await (await RootClient.GetAsync($"api/scoringModels/{model.Id}/json", Ct)).Content.ReadAsByteArrayAsync(Ct);
        var actor = await Actor().WithSystemPermissions(SystemPermission.EditScoringModels).SeedAsync();

        await AssertStatus(HttpStatusCode.Forbidden, await Client(actor).PostAsync("api/scoringModels/json", Upload(exported), Ct));

        await using var context = NewContext();
        Assert.Equal(1, await context.ScoringModels.CountAsync(Ct));
    }

    private async Task<(ScoringModelEntity Model, ScoringCategoryEntity Category)> SeedTemplate()
    {
        var model = TestData.ScoringModel("Template");
        var category = TestData.ScoringCategory(model.Id);
        await Seed(model, category, TestData.ScoringOption(category.Id));

        return (model, category);
    }

    private static MultipartFormDataContent Upload(byte[] file)
    {
        var content = new ByteArrayContent(file);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

        return new MultipartFormDataContent { { content, "ToUpload", "scoring-model.json" } };
    }
}
