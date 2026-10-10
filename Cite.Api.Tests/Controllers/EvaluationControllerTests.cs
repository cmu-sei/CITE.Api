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
/// <c>EvaluationController</c>: each gate is a system permission or the matching evaluation permission on
/// the evaluation in the route.
/// </summary>
public class EvaluationControllerTests(DatabaseFixture fixture, CiteAppFactory factory) : ApiTestBase(fixture, factory)
{
    /// <summary>A request with no identity is answered with a 401; the anonymous client is the case under test.</summary>
    [Fact]
    public async Task GetAll_without_an_identity_is_unauthorized()
    {
        await AssertStatus(HttpStatusCode.Unauthorized, await Client().GetAsync("api/evaluations", Ct));
    }

    [Fact]
    public async Task GetAll_lists_every_evaluation_for_a_caller_holding_ViewEvaluations()
    {
        var graph = await Seed();
        var actor = await Actor().WithSystemPermissions(SystemPermission.ViewEvaluations).SeedAsync();

        var evaluations = await ReadAsync<List<Evaluation>>(await Client(actor).GetAsync("api/evaluations", Ct));

        Assert.Contains(evaluations, x => x.Id == graph.Evaluation.Id);
    }

    /// <summary>Without ViewEvaluations the list holds the evaluations the caller created, and not those it is a member of.</summary>
    [Fact]
    public async Task GetAll_lists_only_the_evaluations_the_caller_created_for_a_caller_holding_ViewEvaluation_on_one()
    {
        var actorId = Guid.NewGuid();
        var member = await Seed("Member Of");
        var created = await TestScenario.SeedEvaluationAsync(Db, Ct, "Created By", createdBy: actorId);
        var actor = await Actor().WithId(actorId).OnEvaluation(member.Evaluation.Id, permissions: [EvaluationPermission.ViewEvaluation]).SeedAsync();

        var evaluations = await ReadAsync<List<Evaluation>>(await Client(actor).GetAsync("api/evaluations", Ct));

        Assert.Equal([created.Evaluation.Id], evaluations.Select(x => x.Id));
    }

    [Fact]
    public async Task GetAll_answers_an_empty_list_when_a_caller_without_ViewEvaluations_names_another_user()
    {
        await TestScenario.SeedEvaluationAsync(Db, Ct, createdBy: Root.Id);
        var actor = await Actor().WithSystemPermissions(SystemPermission.ObserveEvaluations).SeedAsync();

        var evaluations = await ReadAsync<List<Evaluation>>(await Client(actor).GetAsync($"api/evaluations?userId={Root.Id}", Ct));

        Assert.Empty(evaluations);
    }

    /// <summary>The scoringModelId filter compares the scoring model id with each evaluation's creator.</summary>
    [Fact]
    public async Task GetAll_filtered_by_scoring_model_omits_the_evaluation_scored_by_that_model()
    {
        var graph = await Seed();

        var evaluations = await ReadAsync<List<Evaluation>>(
            await RootClient.GetAsync($"api/evaluations?scoringModelId={graph.ScoringModel.Id}", Ct));

        Assert.DoesNotContain(evaluations, x => x.Id == graph.Evaluation.Id);
    }

    [Fact]
    public async Task GetAll_filtered_by_description_lists_the_matching_evaluations()
    {
        var match = await Seed("Findable Exercise");
        var other = await Seed("Other Exercise");

        var evaluations = await ReadAsync<List<Evaluation>>(await RootClient.GetAsync("api/evaluations?description=Findable", Ct));

        Assert.Contains(evaluations, x => x.Id == match.Evaluation.Id);
        Assert.DoesNotContain(evaluations, x => x.Id == other.Evaluation.Id);
    }

    [Fact]
    public async Task GetMine_lists_the_active_evaluations_of_the_callers_teams()
    {
        var active = await Seed("Active One");
        var archived = await Seed("Archived One");
        archived.Evaluation.Status = ItemStatus.Archived;
        await Db.SaveChangesAsync(Ct);
        var actor = await Actor().OnTeam(active.Team.Id).OnTeam(archived.Team.Id).SeedAsync();

        var evaluations = await ReadAsync<List<Evaluation>>(await Client(actor).GetAsync("api/my-evaluations", Ct));

        Assert.Equal([active.Evaluation.Id], evaluations.Select(x => x.Id));
    }

    [Fact]
    public async Task GetMine_omits_an_evaluation_the_caller_holds_a_membership_on_but_no_team_in()
    {
        var actor = await Actor().OnNewEvaluation(EvaluationPermission.ViewEvaluation).SeedAsync();

        var evaluations = await ReadAsync<List<Evaluation>>(await Client(actor).GetAsync("api/my-evaluations", Ct));

        Assert.Empty(evaluations);
    }

    [Fact]
    public async Task Get_returns_the_evaluation_with_the_callers_permissions_to_a_caller_holding_ViewEvaluation_on_it()
    {
        var graph = await Seed();
        var actor = await Actor().OnEvaluation(graph.Evaluation.Id, permissions: [EvaluationPermission.ViewEvaluation, EvaluationPermission.ObserveEvaluation]).SeedAsync();

        var evaluation = await ReadAsync<Evaluation>(await Client(actor).GetAsync($"api/evaluations/{graph.Evaluation.Id}", Ct));

        Assert.Equal(graph.Team.Id, Assert.Single(evaluation.Teams).Id);
        Assert.Equal(["ObserveEvaluation", "ViewEvaluation"], evaluation.EvaluationPermissions.Order());
    }

    [Fact]
    public async Task Get_returns_the_evaluation_to_a_member_of_a_group_holding_ViewEvaluation_on_it()
    {
        var graph = await Seed();
        var group = TestData.Group();
        var role = TestData.EvaluationRole(EvaluationPermission.ViewEvaluation);
        await Seed(group, role, TestData.EvaluationMembership(graph.Evaluation.Id, null, role.Id, group.Id));
        var actor = await Actor().InGroup(group.Id).SeedAsync();

        var evaluation = await ReadAsync<Evaluation>(await Client(actor).GetAsync($"api/evaluations/{graph.Evaluation.Id}", Ct));

        Assert.Equal(["ViewEvaluation"], evaluation.EvaluationPermissions);
    }

    [Fact]
    public async Task Get_is_forbidden_for_a_member_of_a_group_holding_ViewEvaluation_only_on_another_evaluation()
    {
        var graph = await Seed();
        var other = await Seed("Other");
        var group = TestData.Group();
        var role = TestData.EvaluationRole(EvaluationPermission.ViewEvaluation);
        await Seed(group, role, TestData.EvaluationMembership(other.Evaluation.Id, null, role.Id, group.Id));
        var actor = await Actor().InGroup(group.Id).SeedAsync();

        await AssertStatus(HttpStatusCode.Forbidden, await Client(actor).GetAsync($"api/evaluations/{graph.Evaluation.Id}", Ct));
    }

    [Fact]
    public async Task Get_returns_the_evaluation_to_a_caller_holding_ViewEvaluations()
    {
        var graph = await Seed();
        var actor = await Actor().WithSystemPermissions(SystemPermission.ViewEvaluations).SeedAsync();

        await AssertStatus(HttpStatusCode.OK, await Client(actor).GetAsync($"api/evaluations/{graph.Evaluation.Id}", Ct));
    }

    [Fact]
    public async Task Get_is_forbidden_for_a_caller_holding_only_EditEvaluation()
    {
        var graph = await Seed();
        var actor = await Actor().OnEvaluation(graph.Evaluation.Id, permissions: [EvaluationPermission.EditEvaluation]).SeedAsync();

        await AssertApiError(HttpStatusCode.Forbidden, await Client(actor).GetAsync($"api/evaluations/{graph.Evaluation.Id}", Ct));
    }

    [Fact]
    public async Task Get_is_forbidden_for_a_caller_holding_ViewEvaluation_only_on_another_evaluation()
    {
        var graph = await Seed();
        var actor = await Actor().OnNewEvaluation(EvaluationPermission.ViewEvaluation).SeedAsync();

        await AssertStatus(HttpStatusCode.Forbidden, await Client(actor).GetAsync($"api/evaluations/{graph.Evaluation.Id}", Ct));
    }

    /// <summary>A team member who holds no evaluation membership cannot read the evaluation itself.</summary>
    [Fact]
    public async Task Get_is_forbidden_for_a_caller_holding_only_ViewTeam_on_one_of_its_teams()
    {
        var graph = await Seed();
        var actor = await Actor().OnTeam(graph.Team.Id, permissions: [TeamPermission.ViewTeam]).SeedAsync();

        await AssertStatus(HttpStatusCode.Forbidden, await Client(actor).GetAsync($"api/evaluations/{graph.Evaluation.Id}", Ct));
    }

    [Fact]
    public async Task Create_copies_the_scoring_model_adds_a_default_move_and_makes_the_creator_owner()
    {
        var template = TestData.ScoringModel("Template");
        var category = TestData.ScoringCategory(template.Id);
        await Seed(template, category, TestData.ScoringOption(category.Id));
        var actor = await Actor().WithSystemPermissions(SystemPermission.CreateEvaluations).SeedAsync();
        var id = Guid.NewGuid();

        var response = await Client(actor).PostAsJsonAsync("api/evaluations", NewEvaluation(id, template.Id), Ct);

        await AssertStatus(HttpStatusCode.Created, response);
        await using var context = NewContext();
        var stored = await context.Evaluations.Include(x => x.ScoringModel).ThenInclude(x => x.ScoringCategories).Include(x => x.Moves).SingleAsync(x => x.Id == id, Ct);
        Assert.NotEqual(template.Id, stored.ScoringModelId);
        Assert.Equal(id, stored.ScoringModel.EvaluationId);
        Assert.Single(stored.ScoringModel.ScoringCategories);
        Assert.Equal(0, Assert.Single(stored.Moves).MoveNumber);
        var owner = await context.EvaluationMemberships.SingleAsync(x => x.EvaluationId == id, Ct);
        Assert.Equal((actor.Id, TestData.EvaluationRoles.Owner), (owner.UserId.Value, owner.RoleId));
    }

    [Fact]
    public async Task Create_broadcasts_the_new_evaluation_to_its_group()
    {
        var template = TestData.ScoringModel("Template");
        await Seed(template);
        var id = Guid.NewGuid();

        await AssertStatus(HttpStatusCode.Created, await RootClient.PostAsJsonAsync("api/evaluations", NewEvaluation(id, template.Id), Ct));

        var broadcast = Assert.Single(Factory.Hub<MainHub>().ToGroup(id), x => x.Method == MainHubMethods.EvaluationCreated);
        Assert.Equal(id, Assert.IsType<Evaluation>(broadcast.Arguments[0]).Id);
    }

    [Fact]
    public async Task Create_is_forbidden_for_a_caller_holding_only_EditEvaluations()
    {
        var template = TestData.ScoringModel("Template");
        await Seed(template);
        var actor = await Actor().WithSystemPermissions(SystemPermission.EditEvaluations).SeedAsync();
        var id = Guid.NewGuid();

        await AssertStatus(HttpStatusCode.Forbidden, await Client(actor).PostAsJsonAsync("api/evaluations", NewEvaluation(id, template.Id), Ct));

        await using var context = NewContext();
        Assert.False(await context.Evaluations.AnyAsync(x => x.Id == id, Ct));
    }

    [Fact]
    public async Task Create_answers_an_unknown_scoring_model_with_not_found()
    {
        var error = await AssertApiError(HttpStatusCode.NotFound,
            await RootClient.PostAsJsonAsync("api/evaluations", NewEvaluation(Guid.NewGuid(), Guid.NewGuid()), Ct));

        Assert.StartsWith("ScoringModel not found", error.Title);
    }

    [Fact]
    public async Task Copy_creates_an_evaluation_with_copied_teams_and_moves_for_a_caller_holding_CreateEvaluations_and_ViewEvaluation_on_it()
    {
        var graph = await Seed("Source");
        var actor = await Actor().WithName("Copier").WithSystemPermissions(SystemPermission.CreateEvaluations)
            .OnEvaluation(graph.Evaluation.Id, permissions: [EvaluationPermission.ViewEvaluation]).SeedAsync();

        var copy = await ReadAsync<Evaluation>(await Client(actor).PostAsync($"api/evaluations/{graph.Evaluation.Id}/copy", null, Ct));

        Assert.Equal("Source - Copier", copy.Description);
        await using var context = NewContext();
        var stored = await context.Evaluations.Include(x => x.Teams).Include(x => x.Moves).Include(x => x.ScoringModel).SingleAsync(x => x.Id == copy.Id, Ct);
        Assert.NotEqual(graph.Team.Id, Assert.Single(stored.Teams).Id);
        Assert.Single(stored.Moves);
        Assert.Equal(copy.Id, stored.ScoringModel.EvaluationId);
    }

    [Fact]
    public async Task Copy_is_forbidden_for_a_caller_holding_CreateEvaluations_and_ViewEvaluation_only_on_another_evaluation()
    {
        var graph = await Seed();
        var actor = await Actor().WithSystemPermissions(SystemPermission.CreateEvaluations).OnNewEvaluation(EvaluationPermission.ViewEvaluation).SeedAsync();

        await AssertStatus(HttpStatusCode.Forbidden, await Client(actor).PostAsync($"api/evaluations/{graph.Evaluation.Id}/copy", null, Ct));
    }

    [Fact]
    public async Task Copy_is_forbidden_for_a_caller_holding_only_ViewEvaluations()
    {
        var graph = await Seed();
        var actor = await Actor().WithSystemPermissions(SystemPermission.ViewEvaluations).SeedAsync();

        await AssertStatus(HttpStatusCode.Forbidden, await Client(actor).PostAsync($"api/evaluations/{graph.Evaluation.Id}/copy", null, Ct));

        await using var context = NewContext();
        Assert.Equal(1, await context.Evaluations.CountAsync(Ct));
    }

    [Fact]
    public async Task Update_changes_the_description_for_a_caller_holding_EditEvaluation_on_it()
    {
        var graph = await Seed("Before");
        var actor = await Actor().OnEvaluation(graph.Evaluation.Id, permissions: [EvaluationPermission.EditEvaluation]).SeedAsync();

        var response = await Client(actor).PutAsJsonAsync($"api/evaluations/{graph.Evaluation.Id}", Body(graph.Evaluation, "After"), Ct);

        await AssertStatus(HttpStatusCode.OK, response);
        await using var context = NewContext();
        Assert.Equal("After", (await context.Evaluations.SingleAsync(x => x.Id == graph.Evaluation.Id, Ct)).Description);
    }

    [Fact]
    public async Task Update_broadcasts_the_change_to_the_evaluation_group()
    {
        var graph = await Seed("Before");

        await AssertStatus(HttpStatusCode.OK, await RootClient.PutAsJsonAsync($"api/evaluations/{graph.Evaluation.Id}", Body(graph.Evaluation, "After"), Ct));

        var broadcast = Assert.Single(Factory.Hub<MainHub>().ToGroup(graph.Evaluation.Id), x => x.Method == MainHubMethods.EvaluationUpdated);
        Assert.Equal("After", Assert.IsType<Evaluation>(broadcast.Arguments[0]).Description);
        Assert.Contains("description", Assert.IsType<string[]>(broadcast.Arguments[1]));
    }

    [Fact]
    public async Task Update_is_forbidden_for_a_caller_holding_only_ViewEvaluation()
    {
        var graph = await Seed("Before");
        var actor = await Actor().OnEvaluation(graph.Evaluation.Id, permissions: [EvaluationPermission.ViewEvaluation]).SeedAsync();

        await AssertStatus(HttpStatusCode.Forbidden, await Client(actor).PutAsJsonAsync($"api/evaluations/{graph.Evaluation.Id}", Body(graph.Evaluation, "After"), Ct));

        await using var context = NewContext();
        Assert.Equal("Before", (await context.Evaluations.SingleAsync(x => x.Id == graph.Evaluation.Id, Ct)).Description);
    }

    [Fact]
    public async Task Update_is_forbidden_for_a_caller_holding_EditEvaluation_only_on_another_evaluation()
    {
        var graph = await Seed("Before");
        var actor = await Actor().OnNewEvaluation(EvaluationPermission.EditEvaluation).SeedAsync();

        await AssertStatus(HttpStatusCode.Forbidden, await Client(actor).PutAsJsonAsync($"api/evaluations/{graph.Evaluation.Id}", Body(graph.Evaluation, "After"), Ct));
    }

    /// <summary>Activating an evaluation with a user on two of its teams is refused with a 500.</summary>
    [Fact]
    public async Task Update_answers_activating_with_a_user_on_two_teams_with_a_server_error()
    {
        var graph = await Seed();
        graph.Evaluation.Status = ItemStatus.Pending;
        var secondTeam = TestData.Team(graph.Evaluation.Id, graph.TeamType.Id, "Second Team");
        var user = TestData.User(name: "Doubled");
        await Seed(secondTeam, user, TestData.TeamMembership(graph.Team.Id, user.Id), TestData.TeamMembership(secondTeam.Id, user.Id));
        var body = Body(graph.Evaluation, graph.Evaluation.Description) with { Status = "Active" };

        var response = await RootClient.PutAsJsonAsync($"api/evaluations/{graph.Evaluation.Id}", body, Ct);

        var error = await AssertApiError(HttpStatusCode.InternalServerError, response);
        Assert.Contains("User Doubled is on team", error.Detail);
    }

    [Fact]
    public async Task Update_answers_an_unknown_evaluation_with_not_found()
    {
        var graph = await Seed();
        var id = Guid.NewGuid();

        await AssertApiError(HttpStatusCode.NotFound, await RootClient.PutAsJsonAsync($"api/evaluations/{id}", Body(graph.Evaluation, "Nobody") with { Id = id }, Ct));
    }

    [Fact]
    public async Task UpdateSituation_stores_the_situation_for_a_caller_holding_ExecuteEvaluation_on_it()
    {
        var graph = await Seed();
        var actor = await Actor().OnEvaluation(graph.Evaluation.Id, permissions: [EvaluationPermission.ExecuteEvaluation]).SeedAsync();

        var response = await Client(actor).PutAsJsonAsync($"api/evaluations/{graph.Evaluation.Id}/situation",
            new { situationTime = TestData.DefaultDateCreated.AddDays(1), situationDescription = "Escalating" }, Ct);

        await AssertStatus(HttpStatusCode.OK, response);
        await using var context = NewContext();
        Assert.Equal("Escalating", (await context.Evaluations.SingleAsync(x => x.Id == graph.Evaluation.Id, Ct)).SituationDescription);
    }

    [Fact]
    public async Task UpdateSituation_is_forbidden_for_a_caller_holding_only_ObserveEvaluation()
    {
        var graph = await Seed();
        var actor = await Actor().OnEvaluation(graph.Evaluation.Id, permissions: [EvaluationPermission.ObserveEvaluation]).SeedAsync();

        var response = await Client(actor).PutAsJsonAsync($"api/evaluations/{graph.Evaluation.Id}/situation",
            new { situationTime = TestData.DefaultDateCreated, situationDescription = "Escalating" }, Ct);

        await AssertStatus(HttpStatusCode.Forbidden, response);
        await using var context = NewContext();
        Assert.Null((await context.Evaluations.SingleAsync(x => x.Id == graph.Evaluation.Id, Ct)).SituationDescription);
    }

    [Fact]
    public async Task UpdateSituation_stores_the_situation_for_a_caller_holding_ExecuteEvaluations()
    {
        var graph = await Seed();
        var actor = await Actor().WithSystemPermissions(SystemPermission.ExecuteEvaluations).SeedAsync();

        var response = await Client(actor).PutAsJsonAsync($"api/evaluations/{graph.Evaluation.Id}/situation",
            new { situationTime = TestData.DefaultDateCreated.AddDays(1), situationDescription = "Escalating" }, Ct);

        await AssertStatus(HttpStatusCode.OK, response);
        await using var context = NewContext();
        Assert.Equal("Escalating", (await context.Evaluations.SingleAsync(x => x.Id == graph.Evaluation.Id, Ct)).SituationDescription);
    }

    [Fact]
    public async Task UpdateSituation_is_forbidden_for_a_caller_holding_only_ObserveEvaluations()
    {
        var graph = await Seed();
        var actor = await Actor().WithSystemPermissions(SystemPermission.ObserveEvaluations).SeedAsync();

        var response = await Client(actor).PutAsJsonAsync($"api/evaluations/{graph.Evaluation.Id}/situation",
            new { situationTime = TestData.DefaultDateCreated, situationDescription = "Escalating" }, Ct);

        await AssertStatus(HttpStatusCode.Forbidden, response);
        await using var context = NewContext();
        Assert.Null((await context.Evaluations.SingleAsync(x => x.Id == graph.Evaluation.Id, Ct)).SituationDescription);
    }

    [Fact]
    public async Task SetCurrentMove_moves_the_evaluation_and_takes_the_move_situation_for_a_caller_holding_ExecuteEvaluation_on_it()
    {
        var graph = await Seed();
        var move = TestData.Move(graph.Evaluation.Id, 1);
        move.SituationDescription = "Move One Situation";
        await Seed(move);
        var actor = await Actor().OnEvaluation(graph.Evaluation.Id, permissions: [EvaluationPermission.ExecuteEvaluation]).SeedAsync();

        await AssertStatus(HttpStatusCode.OK, await Client(actor).PutAsync($"api/evaluations/{graph.Evaluation.Id}/move/1", null, Ct));

        await using var context = NewContext();
        var stored = await context.Evaluations.SingleAsync(x => x.Id == graph.Evaluation.Id, Ct);
        Assert.Equal((1, "Move One Situation"), (stored.CurrentMoveNumber, stored.SituationDescription));
    }

    [Fact]
    public async Task SetCurrentMove_is_forbidden_for_a_caller_holding_only_ObserveEvaluation()
    {
        var graph = await Seed();
        await Seed(TestData.Move(graph.Evaluation.Id, 1));
        var actor = await Actor().OnEvaluation(graph.Evaluation.Id, permissions: [EvaluationPermission.ObserveEvaluation]).SeedAsync();

        await AssertStatus(HttpStatusCode.Forbidden, await Client(actor).PutAsync($"api/evaluations/{graph.Evaluation.Id}/move/1", null, Ct));

        await using var context = NewContext();
        Assert.Equal(0, (await context.Evaluations.SingleAsync(x => x.Id == graph.Evaluation.Id, Ct)).CurrentMoveNumber);
    }

    [Fact]
    public async Task SetCurrentMove_moves_the_evaluation_and_takes_the_move_situation_for_a_caller_holding_ExecuteEvaluations()
    {
        var graph = await Seed();
        var move = TestData.Move(graph.Evaluation.Id, 1);
        move.SituationDescription = "Move One Situation";
        await Seed(move);
        var actor = await Actor().WithSystemPermissions(SystemPermission.ExecuteEvaluations).SeedAsync();

        await AssertStatus(HttpStatusCode.OK, await Client(actor).PutAsync($"api/evaluations/{graph.Evaluation.Id}/move/1", null, Ct));

        await using var context = NewContext();
        var stored = await context.Evaluations.SingleAsync(x => x.Id == graph.Evaluation.Id, Ct);
        Assert.Equal((1, "Move One Situation"), (stored.CurrentMoveNumber, stored.SituationDescription));
    }

    [Fact]
    public async Task SetCurrentMove_is_forbidden_for_a_caller_holding_only_ObserveEvaluations()
    {
        var graph = await Seed();
        await Seed(TestData.Move(graph.Evaluation.Id, 1));
        var actor = await Actor().WithSystemPermissions(SystemPermission.ObserveEvaluations).SeedAsync();

        await AssertStatus(HttpStatusCode.Forbidden, await Client(actor).PutAsync($"api/evaluations/{graph.Evaluation.Id}/move/1", null, Ct));

        await using var context = NewContext();
        Assert.Equal(0, (await context.Evaluations.SingleAsync(x => x.Id == graph.Evaluation.Id, Ct)).CurrentMoveNumber);
    }

    [Fact]
    public async Task SetCurrentMove_answers_a_move_the_evaluation_does_not_have_with_not_found()
    {
        var graph = await Seed();

        var error = await AssertApiError(HttpStatusCode.NotFound, await RootClient.PutAsync($"api/evaluations/{graph.Evaluation.Id}/move/7", null, Ct));

        Assert.Equal("Move not found", error.Title);
    }

    [Fact]
    public async Task Delete_removes_the_evaluation_and_its_scoring_model_copy_for_a_caller_holding_ManageEvaluation_on_it()
    {
        var graph = await Seed();
        var actor = await Actor().OnEvaluation(graph.Evaluation.Id, permissions: [EvaluationPermission.ManageEvaluation]).SeedAsync();

        await AssertStatus(HttpStatusCode.NoContent, await Client(actor).DeleteAsync($"api/evaluations/{graph.Evaluation.Id}", Ct));

        await using var context = NewContext();
        Assert.False(await context.Evaluations.AnyAsync(x => x.Id == graph.Evaluation.Id, Ct));
        Assert.False(await context.ScoringModels.AnyAsync(x => x.Id == graph.ScoringModel.Id, Ct));
    }

    [Fact]
    public async Task Delete_broadcasts_the_id_to_the_evaluation_group()
    {
        var graph = await Seed();

        await AssertStatus(HttpStatusCode.NoContent, await RootClient.DeleteAsync($"api/evaluations/{graph.Evaluation.Id}", Ct));

        var broadcast = Assert.Single(Factory.Hub<MainHub>().ToGroup(graph.Evaluation.Id), x => x.Method == MainHubMethods.EvaluationDeleted);
        Assert.Equal(graph.Evaluation.Id, broadcast.Argument);
    }

    [Fact]
    public async Task Delete_is_forbidden_for_a_caller_holding_only_EditEvaluation()
    {
        var graph = await Seed();
        var actor = await Actor().OnEvaluation(graph.Evaluation.Id, permissions: [EvaluationPermission.EditEvaluation]).SeedAsync();

        await AssertStatus(HttpStatusCode.Forbidden, await Client(actor).DeleteAsync($"api/evaluations/{graph.Evaluation.Id}", Ct));

        await using var context = NewContext();
        Assert.True(await context.Evaluations.AnyAsync(x => x.Id == graph.Evaluation.Id, Ct));
    }

    [Fact]
    public async Task Delete_is_forbidden_for_a_caller_holding_ManageEvaluation_only_on_another_evaluation()
    {
        var graph = await Seed();
        var actor = await Actor().OnNewEvaluation(EvaluationPermission.ManageEvaluation).SeedAsync();

        await AssertStatus(HttpStatusCode.Forbidden, await Client(actor).DeleteAsync($"api/evaluations/{graph.Evaluation.Id}", Ct));
    }

    [Fact]
    public async Task DownloadJson_answers_a_file_named_for_the_evaluation_to_a_caller_holding_ViewEvaluation_on_it()
    {
        var graph = await Seed("Exported");
        var actor = await Actor().OnEvaluation(graph.Evaluation.Id, permissions: [EvaluationPermission.ViewEvaluation]).SeedAsync();

        var response = await Client(actor).GetAsync($"api/evaluations/{graph.Evaluation.Id}/json", Ct);

        await AssertStatus(HttpStatusCode.OK, response);
        Assert.Equal("Exported.json", response.Content.Headers.ContentDisposition?.FileName);
        Assert.Contains(graph.Team.Id.ToString(), await response.Content.ReadAsStringAsync(Ct));
    }

    [Fact]
    public async Task DownloadJson_is_forbidden_for_a_caller_holding_ViewEvaluation_only_on_another_evaluation()
    {
        var graph = await Seed();
        var actor = await Actor().OnNewEvaluation(EvaluationPermission.ViewEvaluation).SeedAsync();

        await AssertStatus(HttpStatusCode.Forbidden, await Client(actor).GetAsync($"api/evaluations/{graph.Evaluation.Id}/json", Ct));
    }

    [Fact]
    public async Task UploadJson_imports_a_downloaded_evaluation_as_a_new_one_for_a_caller_holding_CreateEvaluations()
    {
        var graph = await Seed("Round Trip");
        var exported = await (await RootClient.GetAsync($"api/evaluations/{graph.Evaluation.Id}/json", Ct)).Content.ReadAsByteArrayAsync(Ct);
        var actor = await Actor().WithName("Importer").WithSystemPermissions(SystemPermission.CreateEvaluations).SeedAsync();

        var imported = await ReadAsync<Evaluation>(await Client(actor).PostAsync("api/evaluations/json", Upload(exported), Ct));

        Assert.NotEqual(graph.Evaluation.Id, imported.Id);
        Assert.Equal("Round Trip - Importer", imported.Description);
        await using var context = NewContext();
        Assert.Equal(1, await context.Teams.CountAsync(x => x.EvaluationId == imported.Id, Ct));
    }

    [Fact]
    public async Task UploadJson_is_forbidden_for_a_caller_holding_only_EditEvaluations()
    {
        var graph = await Seed("Round Trip");
        var exported = await (await RootClient.GetAsync($"api/evaluations/{graph.Evaluation.Id}/json", Ct)).Content.ReadAsByteArrayAsync(Ct);
        var actor = await Actor().WithSystemPermissions(SystemPermission.EditEvaluations).SeedAsync();

        await AssertStatus(HttpStatusCode.Forbidden, await Client(actor).PostAsync("api/evaluations/json", Upload(exported), Ct));

        await using var context = NewContext();
        Assert.Equal(1, await context.Evaluations.CountAsync(Ct));
    }

    private Task<EvaluationGraph> Seed(string description = "Test Evaluation") =>
        TestScenario.SeedEvaluationAsync(Db, Ct, description);

    private static object NewEvaluation(Guid id, Guid scoringModelId) => new
    {
        id,
        description = "New Evaluation",
        status = "Active",
        scoringModelId,
        situationTime = TestData.DefaultDateCreated
    };

    private static EvaluationBody Body(EvaluationEntity evaluation, string description) =>
        new(evaluation.Id, description, evaluation.Status.ToString(), evaluation.ScoringModelId, evaluation.SituationTime);

    private static MultipartFormDataContent Upload(byte[] file)
    {
        var content = new ByteArrayContent(file);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

        return new MultipartFormDataContent { { content, "ToUpload", "evaluation.json" } };
    }

    private sealed record EvaluationBody(Guid Id, string Description, string Status, Guid ScoringModelId, DateTime SituationTime);
}
