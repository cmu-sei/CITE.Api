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

    /// <summary>The statement's move grouping is taken from another evaluation's move with the current move number.</summary>
    [Fact]
    public async Task AddUserAsync_groups_the_statement_under_another_evaluations_move()
    {
        var graph = await TestScenario.SeedEvaluationAsync(Db, Ct);
        graph.Evaluation.CurrentMoveNumber = 3;
        var other = await TestScenario.SeedEvaluationAsync(Db, Ct, "Other");
        await Seed(TestData.Move(other.Evaluation.Id, 3, "Other Evaluation's Move"));
        var caller = TestData.User(name: "Caller");
        var assignee = TestData.User(name: "Assignee");
        var duty = TestData.Duty(graph.Evaluation.Id, graph.Team.Id);
        await Seed(caller, assignee, TestData.TeamMembership(graph.Team.Id, caller.Id), duty);
        List<Dictionary<string, string>> grouping = null;
        _xApi.IsConfigured().Returns(true);
        await _xApi.CreateAsync(Arg.Any<Uri>(), Arg.Any<Dictionary<string, string>>(), Arg.Any<Dictionary<string, string>>(),
            Arg.Any<Dictionary<string, string>>(), Arg.Do<List<Dictionary<string, string>>>(x => grouping = x),
            Arg.Any<Dictionary<string, string>>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());

        await using var context = NewContext();
        await Service(context, caller).AddUserAsync(duty.Id, assignee.Id, Ct);

        Assert.Equal("Other Evaluation's Move", Assert.Single(grouping)["description"]);
    }

    private DutyService Service(Cite.Api.Data.CiteContext context, UserEntity user) =>
        new(context, AuthorizationHarness.CreateFrameworkAuthorizationService(),
            new ClaimsPrincipalBuilder().WithUserId(user.Id).Build(), TestMapper.Mapper, _xApi, new DatabaseOptions());
}
