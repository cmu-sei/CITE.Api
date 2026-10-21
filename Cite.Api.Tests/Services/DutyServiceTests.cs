// Copyright 2026 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.

using Cite.Api.Data.Models;
using Cite.Api.Infrastructure.Options;
using Cite.Api.Services;
using Cite.Api.Tests.Support;

namespace Cite.Api.Tests.Services;

/// <summary>
/// <see cref="DutyService"/>'s xAPI statement, built only with xAPI configured. The xAPI service is a
/// substitute this test owns, reporting itself configured, and the statement is read off the call.
/// </summary>
public class DutyServiceTests(DatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    private readonly IXApiService _xApi = Substitute.For<IXApiService>();
    private List<Dictionary<string, string>> _grouping;

    /// <summary>
    /// The statement's move grouping is the duty's own evaluation's current move, not another evaluation's
    /// move with the same number (seeded first, so an unscoped lookup would find it).
    /// </summary>
    [Fact]
    public async Task AddUserAsync_groups_the_statement_under_its_own_evaluations_move()
    {
        var other = await TestScenario.SeedEvaluationAsync(Db, Ct, "Other");
        await Seed(TestData.Move(other.Evaluation.Id, 3, "Other Evaluation's Move"));
        var graph = await TestScenario.SeedEvaluationAsync(Db, Ct);
        graph.Evaluation.CurrentMoveNumber = 3;
        await Seed(TestData.Move(graph.Evaluation.Id, 3, "This Evaluation's Move"));
        var (caller, assignee, duty) = await SeedDuty(graph);
        CaptureGrouping();

        await using var context = NewContext();
        await Service(context, caller).AddUserAsync(duty.Id, assignee.Id, Ct);

        var grouping = Assert.Single(_grouping);
        Assert.Equal("3", grouping["id"]);
        Assert.Equal("This Evaluation's Move", grouping["description"]);
    }

    /// <summary>
    /// An evaluation with no move at its current move number still logs the statement, grouped under the move
    /// number with no description, rather than borrowing another evaluation's move or failing.
    /// </summary>
    [Fact]
    public async Task AddUserAsync_groups_by_move_number_when_the_evaluation_has_no_such_move()
    {
        var other = await TestScenario.SeedEvaluationAsync(Db, Ct, "Other");
        await Seed(TestData.Move(other.Evaluation.Id, 3, "Other Evaluation's Move"));
        var graph = await TestScenario.SeedEvaluationAsync(Db, Ct);
        graph.Evaluation.CurrentMoveNumber = 3;
        var (caller, assignee, duty) = await SeedDuty(graph);
        CaptureGrouping();

        await using var context = NewContext();
        await Service(context, caller).AddUserAsync(duty.Id, assignee.Id, Ct);

        var grouping = Assert.Single(_grouping);
        Assert.Equal("3", grouping["id"]);
        Assert.Equal("Move 3", grouping["name"]);
        Assert.Equal("", grouping["description"]);
    }

    private async Task<(UserEntity Caller, UserEntity Assignee, DutyEntity Duty)> SeedDuty(EvaluationGraph graph)
    {
        var caller = TestData.User(name: "Caller");
        var assignee = TestData.User(name: "Assignee");
        var duty = TestData.Duty(graph.Evaluation.Id, graph.Team.Id);
        await Seed(caller, assignee, TestData.TeamMembership(graph.Team.Id, caller.Id), duty);
        return (caller, assignee, duty);
    }

    private void CaptureGrouping()
    {
        _xApi.IsConfigured().Returns(true);
        _xApi.CreateAsync(Arg.Any<Uri>(), Arg.Any<Dictionary<string, string>>(), Arg.Any<Dictionary<string, string>>(),
            Arg.Any<Dictionary<string, string>>(), Arg.Do<List<Dictionary<string, string>>>(x => _grouping = x),
            Arg.Any<Dictionary<string, string>>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(true);
    }

    private DutyService Service(Cite.Api.Data.CiteContext context, UserEntity user) =>
        new(context, AuthorizationHarness.CreateFrameworkAuthorizationService(),
            new ClaimsPrincipalBuilder().WithUserId(user.Id).Build(), TestMapper.Mapper, _xApi, new DatabaseOptions());
}
