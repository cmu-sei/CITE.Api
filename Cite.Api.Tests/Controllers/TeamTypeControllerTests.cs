// Copyright 2026 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.

using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Cite.Api.Data.Enumerations;
using Cite.Api.Tests.Support;
using Cite.Api.ViewModels;

namespace Cite.Api.Tests.Controllers;

/// <summary><c>TeamTypeController</c>: team types, readable by any caller and managed with ManageTeamTypes.</summary>
public class TeamTypeControllerTests(DatabaseFixture fixture, CiteAppFactory factory) : ApiTestBase(fixture, factory)
{
    /// <summary>A request with no identity is answered with a 401; the anonymous client is the case under test.</summary>
    [Fact]
    public async Task GetAll_without_an_identity_is_unauthorized()
    {
        await AssertStatus(HttpStatusCode.Unauthorized, await Client().GetAsync("api/teamTypes", Ct));
    }

    /// <summary>No permission is checked: a caller holding no permission at all lists the team types.</summary>
    [Fact]
    public async Task GetAll_lists_the_team_types_for_any_authenticated_caller()
    {
        var teamType = TestData.TeamType("Listed Type");
        await Seed(teamType);
        var actor = await Actor().SeedAsync();

        var teamTypes = await ReadAsync<List<TeamType>>(await Client(actor).GetAsync("api/teamTypes", Ct));

        Assert.Contains(teamTypes, x => x.Id == teamType.Id && x.Name == "Listed Type");
    }

    /// <summary>No permission is checked: a caller holding no permission at all reads a team type.</summary>
    [Fact]
    public async Task Get_returns_the_team_type_to_any_authenticated_caller()
    {
        var teamType = TestData.TeamType("Read Type", isOfficialScoreContributor: true);
        await Seed(teamType);
        var actor = await Actor().SeedAsync();

        var read = await ReadAsync<TeamType>(await Client(actor).GetAsync($"api/teamTypes/{teamType.Id}", Ct));

        Assert.True(read.IsOfficialScoreContributor);
    }

    [Fact]
    public async Task Get_answers_an_unknown_team_type_with_not_found()
    {
        await AssertApiError(HttpStatusCode.NotFound, await RootClient.GetAsync($"api/teamTypes/{Guid.NewGuid()}", Ct));
    }

    [Fact]
    public async Task Create_persists_the_team_type_for_a_caller_holding_ManageTeamTypes()
    {
        var actor = await Actor().WithSystemPermissions(SystemPermission.ManageTeamTypes).SeedAsync();

        var response = await Client(actor).PostAsJsonAsync("api/teamTypes", new { name = "Created Type", showTeamTypeAverage = true }, Ct);

        var created = await ReadAsync<TeamType>(response);
        await using var context = NewContext();
        var stored = await context.TeamTypes.SingleAsync(x => x.Id == created.Id, Ct);
        Assert.True(stored.ShowTeamTypeAverage);
        Assert.Equal(actor.Id, stored.CreatedBy);
    }

    [Fact]
    public async Task Create_is_forbidden_for_a_caller_holding_only_ViewTeamTypes()
    {
        var actor = await Actor().WithSystemPermissions(SystemPermission.ViewTeamTypes).SeedAsync();

        var response = await Client(actor).PostAsJsonAsync("api/teamTypes", new { name = "Refused Type" }, Ct);

        await AssertStatus(HttpStatusCode.Forbidden, response);
        await using var context = NewContext();
        Assert.False(await context.TeamTypes.AnyAsync(x => x.Name == "Refused Type", Ct));
    }

    [Fact]
    public async Task Update_renames_the_team_type_for_a_caller_holding_ManageTeamTypes()
    {
        var teamType = TestData.TeamType("Before");
        await Seed(teamType);
        var actor = await Actor().WithSystemPermissions(SystemPermission.ManageTeamTypes).SeedAsync();

        var response = await Client(actor).PutAsJsonAsync($"api/teamTypes/{teamType.Id}", new { id = teamType.Id, name = "After" }, Ct);

        await AssertStatus(HttpStatusCode.OK, response);
        await using var context = NewContext();
        Assert.Equal("After", (await context.TeamTypes.SingleAsync(x => x.Id == teamType.Id, Ct)).Name);
    }

    [Fact]
    public async Task Update_is_forbidden_for_a_caller_holding_only_ViewTeamTypes()
    {
        var teamType = TestData.TeamType("Before");
        await Seed(teamType);
        var actor = await Actor().WithSystemPermissions(SystemPermission.ViewTeamTypes).SeedAsync();

        var response = await Client(actor).PutAsJsonAsync($"api/teamTypes/{teamType.Id}", new { id = teamType.Id, name = "After" }, Ct);

        await AssertStatus(HttpStatusCode.Forbidden, response);
        await using var context = NewContext();
        Assert.Equal("Before", (await context.TeamTypes.SingleAsync(x => x.Id == teamType.Id, Ct)).Name);
    }

    [Fact]
    public async Task Delete_removes_the_team_type_for_a_caller_holding_ManageTeamTypes()
    {
        var teamType = TestData.TeamType();
        await Seed(teamType);
        var actor = await Actor().WithSystemPermissions(SystemPermission.ManageTeamTypes).SeedAsync();

        await AssertStatus(HttpStatusCode.NoContent, await Client(actor).DeleteAsync($"api/teamTypes/{teamType.Id}", Ct));

        await using var context = NewContext();
        Assert.False(await context.TeamTypes.AnyAsync(x => x.Id == teamType.Id, Ct));
    }

    [Fact]
    public async Task Delete_is_forbidden_for_a_caller_holding_only_ViewTeamTypes()
    {
        var teamType = TestData.TeamType();
        await Seed(teamType);
        var actor = await Actor().WithSystemPermissions(SystemPermission.ViewTeamTypes).SeedAsync();

        await AssertStatus(HttpStatusCode.Forbidden, await Client(actor).DeleteAsync($"api/teamTypes/{teamType.Id}", Ct));

        await using var context = NewContext();
        Assert.True(await context.TeamTypes.AnyAsync(x => x.Id == teamType.Id, Ct));
    }

    /// <summary>Deleting a team type deletes every team of that type with it, through the cascade on teams.team_type_id.</summary>
    [Fact]
    public async Task Delete_removes_the_teams_of_that_type_with_it()
    {
        var scoringModel = TestData.ScoringModel();
        var evaluation = TestData.Evaluation(scoringModel.Id);
        var teamType = TestData.TeamType();
        var team = TestData.Team(evaluation.Id, teamType.Id);
        await Seed(scoringModel, evaluation, teamType, team);

        await AssertStatus(HttpStatusCode.NoContent, await RootClient.DeleteAsync($"api/teamTypes/{teamType.Id}", Ct));

        await using var context = NewContext();
        Assert.False(await context.Teams.AnyAsync(x => x.Id == team.Id, Ct));
    }
}
