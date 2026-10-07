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

/// <summary><c>UserController</c>: users, gated by ViewUsers and ManageUsers, and an evaluation's users.</summary>
public class UserControllerTests(DatabaseFixture fixture, CiteAppFactory factory) : ApiTestBase(fixture, factory)
{
    /// <summary>A request with no identity is answered with a 401; the anonymous client is the case under test.</summary>
    [Fact]
    public async Task GetAll_without_an_identity_is_unauthorized()
    {
        await AssertStatus(HttpStatusCode.Unauthorized, await Client().GetAsync("api/users", Ct));
    }

    [Fact]
    public async Task GetAll_lists_the_users_for_a_caller_holding_ViewUsers()
    {
        var user = TestData.User(name: "Listed User");
        await Seed(user);
        var actor = await Actor().WithSystemPermissions(SystemPermission.ViewUsers).SeedAsync();

        var users = await ReadAsync<List<User>>(await Client(actor).GetAsync("api/users", Ct));

        Assert.Contains(users, x => x.Id == user.Id && x.Name == "Listed User");
    }

    /// <summary>ViewEvaluations alone is enough to list every user.</summary>
    [Fact]
    public async Task GetAll_lists_the_users_for_a_caller_holding_only_ViewEvaluations()
    {
        var user = TestData.User();
        await Seed(user);
        var actor = await Actor().WithSystemPermissions(SystemPermission.ViewEvaluations).SeedAsync();

        var users = await ReadAsync<List<User>>(await Client(actor).GetAsync("api/users", Ct));

        Assert.Contains(users, x => x.Id == user.Id);
    }

    [Fact]
    public async Task GetAll_is_forbidden_for_a_caller_holding_only_ManageUsers()
    {
        var actor = await Actor().WithSystemPermissions(SystemPermission.ManageUsers).SeedAsync();

        await AssertStatus(HttpStatusCode.Forbidden, await Client(actor).GetAsync("api/users", Ct));
    }

    [Fact]
    public async Task Get_returns_the_user_to_a_caller_holding_ViewUsers()
    {
        var user = TestData.User(name: "Read User");
        await Seed(user);
        var actor = await Actor().WithSystemPermissions(SystemPermission.ViewUsers).SeedAsync();

        var read = await ReadAsync<User>(await Client(actor).GetAsync($"api/users/{user.Id}", Ct));

        Assert.Equal("Read User", read.Name);
    }

    [Fact]
    public async Task Get_is_forbidden_for_a_caller_holding_only_ViewEvaluations()
    {
        var user = TestData.User();
        await Seed(user);
        var actor = await Actor().WithSystemPermissions(SystemPermission.ViewEvaluations).SeedAsync();

        await AssertStatus(HttpStatusCode.Forbidden, await Client(actor).GetAsync($"api/users/{user.Id}", Ct));
    }

    [Fact]
    public async Task Create_persists_the_user_and_broadcasts_it_to_the_creator_for_a_caller_holding_ManageUsers()
    {
        var actor = await Actor().WithSystemPermissions(SystemPermission.ManageUsers).SeedAsync();
        var id = Guid.NewGuid();

        var response = await Client(actor).PostAsJsonAsync("api/users", new { id, name = "New User" }, Ct);

        await AssertStatus(HttpStatusCode.Created, response);
        await using var context = NewContext();
        Assert.Equal("New User", (await context.Users.SingleAsync(x => x.Id == id, Ct)).Name);
        var broadcast = Assert.Single(Factory.Hub<MainHub>().ToGroup(actor.Id), x => x.Method == MainHubMethods.UserCreated);
        Assert.Equal(id, Assert.IsType<User>(broadcast.Arguments[0]).Id);
    }

    [Fact]
    public async Task Create_is_forbidden_for_a_caller_holding_only_ViewUsers()
    {
        var actor = await Actor().WithSystemPermissions(SystemPermission.ViewUsers).SeedAsync();
        var id = Guid.NewGuid();

        var response = await Client(actor).PostAsJsonAsync("api/users", new { id, name = "Refused User" }, Ct);

        await AssertStatus(HttpStatusCode.Forbidden, response);
        await using var context = NewContext();
        Assert.False(await context.Users.AnyAsync(x => x.Id == id, Ct));
    }

    /// <summary>A user posted without an id is saved under a generated id and the request is answered with a 500.</summary>
    [Fact]
    public async Task Create_without_an_id_saves_the_user_and_answers_with_a_server_error()
    {
        var response = await RootClient.PostAsJsonAsync("api/users", new { name = "Idless User" }, Ct);

        var error = await AssertApiError(HttpStatusCode.InternalServerError, response);
        Assert.Equal("Object reference not set to an instance of an object.", error.Detail);
        await using var context = NewContext();
        Assert.True(await context.Users.AnyAsync(x => x.Name == "Idless User", Ct));
    }

    /// <summary>An id another user already has is answered with a 500.</summary>
    [Fact]
    public async Task Create_answers_an_id_that_is_already_taken_with_a_server_error()
    {
        var user = TestData.User();
        await Seed(user);

        var response = await RootClient.PostAsJsonAsync("api/users", new { id = user.Id, name = "Duplicate" }, Ct);

        var error = await AssertApiError(HttpStatusCode.InternalServerError, response);
        Assert.StartsWith("An error occurred while saving the entity changes.", error.Detail);
    }

    [Fact]
    public async Task Update_assigns_a_role_for_a_caller_holding_ManageUsers()
    {
        var user = TestData.User(name: "Assigned");
        await Seed(user);
        var actor = await Actor().WithSystemPermissions(SystemPermission.ManageUsers).SeedAsync();

        var response = await Client(actor).PutAsJsonAsync($"api/users/{user.Id}",
            new { id = user.Id, name = "Assigned", roleId = TestData.Roles.Observer.ToString() }, Ct);

        await AssertStatus(HttpStatusCode.OK, response);
        await using var context = NewContext();
        Assert.Equal(TestData.Roles.Observer, (await context.Users.SingleAsync(x => x.Id == user.Id, Ct)).RoleId);
    }

    [Fact]
    public async Task Update_is_forbidden_for_a_caller_holding_only_ViewUsers()
    {
        var user = TestData.User(name: "Unassigned");
        await Seed(user);
        var actor = await Actor().WithSystemPermissions(SystemPermission.ViewUsers).SeedAsync();

        var response = await Client(actor).PutAsJsonAsync($"api/users/{user.Id}",
            new { id = user.Id, name = "Unassigned", roleId = TestData.Roles.Administrator.ToString() }, Ct);

        await AssertStatus(HttpStatusCode.Forbidden, response);
        await using var context = NewContext();
        Assert.Null((await context.Users.SingleAsync(x => x.Id == user.Id, Ct)).RoleId);
    }

    [Fact]
    public async Task Update_refuses_to_change_the_callers_own_id()
    {
        var response = await RootClient.PutAsJsonAsync($"api/users/{Root.Id}", new { id = Guid.NewGuid(), name = Root.Name }, Ct);

        var error = await AssertApiError(HttpStatusCode.Forbidden, response);
        Assert.Equal("You cannot change your own Id", error.Title);
    }

    [Fact]
    public async Task Update_answers_an_unknown_user_with_not_found()
    {
        var id = Guid.NewGuid();

        await AssertApiError(HttpStatusCode.NotFound, await RootClient.PutAsJsonAsync($"api/users/{id}", new { id, name = "Nobody" }, Ct));
    }

    [Fact]
    public async Task Delete_removes_the_user_for_a_caller_holding_ManageUsers()
    {
        var user = TestData.User();
        await Seed(user);
        var actor = await Actor().WithSystemPermissions(SystemPermission.ManageUsers).SeedAsync();

        await AssertStatus(HttpStatusCode.NoContent, await Client(actor).DeleteAsync($"api/users/{user.Id}", Ct));

        await using var context = NewContext();
        Assert.False(await context.Users.AnyAsync(x => x.Id == user.Id, Ct));
    }

    [Fact]
    public async Task Delete_is_forbidden_for_a_caller_holding_only_ViewUsers()
    {
        var user = TestData.User();
        await Seed(user);
        var actor = await Actor().WithSystemPermissions(SystemPermission.ViewUsers).SeedAsync();

        await AssertStatus(HttpStatusCode.Forbidden, await Client(actor).DeleteAsync($"api/users/{user.Id}", Ct));

        await using var context = NewContext();
        Assert.True(await context.Users.AnyAsync(x => x.Id == user.Id, Ct));
    }

    [Fact]
    public async Task Delete_refuses_the_callers_own_account()
    {
        var error = await AssertApiError(HttpStatusCode.Forbidden, await RootClient.DeleteAsync($"api/users/{Root.Id}", Ct));

        Assert.Equal("You cannot delete your own account", error.Title);
    }

    [Fact]
    public async Task GetEvaluationUsers_lists_the_team_members_to_a_member_of_one_of_its_teams()
    {
        var (evaluation, team) = await SeedEvaluationWithTeam();
        var teammate = TestData.User(name: "Teammate");
        await Seed(teammate, TestData.TeamMembership(team.Id, teammate.Id));
        var actor = await Actor().OnTeam(team.Id).SeedAsync();

        var users = await ReadAsync<List<UserIdentity>>(await Client(actor).GetAsync($"api/evaluations/{evaluation.Id}/users", Ct));

        Assert.Equal(new[] { actor.Id, teammate.Id }.Order(), users.Select(x => x.Id).Order());
    }

    [Fact]
    public async Task GetEvaluationUsers_lists_the_team_members_to_a_caller_holding_ObserveEvaluations()
    {
        var (evaluation, team) = await SeedEvaluationWithTeam();
        var member = TestData.User();
        await Seed(member, TestData.TeamMembership(team.Id, member.Id));
        var actor = await Actor().WithSystemPermissions(SystemPermission.ObserveEvaluations).SeedAsync();

        var users = await ReadAsync<List<UserIdentity>>(await Client(actor).GetAsync($"api/evaluations/{evaluation.Id}/users", Ct));

        Assert.Equal(member.Id, Assert.Single(users).Id);
    }

    [Fact]
    public async Task GetEvaluationUsers_is_forbidden_for_a_caller_holding_ViewTeam_only_on_a_team_in_another_evaluation()
    {
        var (evaluation, _) = await SeedEvaluationWithTeam();
        var (otherEvaluation, _) = await SeedEvaluationWithTeam();
        var actor = await Actor().OnNewTeam(otherEvaluation.Id, TeamPermission.ViewTeam).SeedAsync();

        await AssertApiError(HttpStatusCode.Forbidden, await Client(actor).GetAsync($"api/evaluations/{evaluation.Id}/users", Ct));
    }

    private async Task<(EvaluationEntity Evaluation, TeamEntity Team)> SeedEvaluationWithTeam()
    {
        var scoringModel = TestData.ScoringModel();
        var evaluation = TestData.Evaluation(scoringModel.Id);
        var teamType = TestData.TeamType();
        var team = TestData.Team(evaluation.Id, teamType.Id);
        await Seed(scoringModel, evaluation, teamType, team);

        return (evaluation, team);
    }
}
