// Copyright 2026 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.

using Cite.Api.Data;
using Cite.Api.Data.Models;

namespace Cite.Api.Tests.Support;

/// <summary>What <see cref="TestScenario.SeedEvaluationAsync"/> seeded.</summary>
public sealed record EvaluationGraph(
    ScoringModelEntity ScoringModel,
    ScoringCategoryEntity Category,
    ScoringOptionEntity Option,
    EvaluationEntity Evaluation,
    MoveEntity Move,
    TeamTypeEntity TeamType,
    TeamEntity Team);

/// <summary>What <see cref="TestScenario.SeedSubmissionAsync"/> seeded.</summary>
public sealed record SubmissionGraph(
    SubmissionEntity Submission,
    SubmissionCategoryEntity Category,
    SubmissionOptionEntity Option);

/// <summary>
/// Seeds the shape most cite.api tests start from, as <c>EvaluationService.CreateAsync</c> leaves it: an
/// evaluation with its own copy of a scoring model (one category, one option) pointing back at it, move 0,
/// and one team.
/// </summary>
/// <remarks>
/// The rows go through a test's own context, whose mediator is a substitute, so no entity event handler
/// runs: seeding creates no submissions and broadcasts nothing.
/// </remarks>
public static class TestScenario
{
    public static async Task<EvaluationGraph> SeedEvaluationAsync(
        CiteContext db,
        CancellationToken ct,
        string description = "Test Evaluation",
        bool officialScoreContributor = false,
        Guid? createdBy = null)
    {
        var scoringModel = TestData.ScoringModel($"{description} Scoring Model");
        var category = TestData.ScoringCategory(scoringModel.Id);
        var option = TestData.ScoringOption(category.Id);
        var evaluation = TestData.Evaluation(scoringModel.Id, description);
        // Set before the first save: CiteContext restores CreatedBy from the original values on update.
        evaluation.CreatedBy = createdBy ?? Guid.Empty;
        var move = TestData.Move(evaluation.Id);
        var teamType = TestData.TeamType(isOfficialScoreContributor: officialScoreContributor);
        var team = TestData.Team(evaluation.Id, teamType.Id, $"{description} Team");

        db.AddRange(scoringModel, category, option, evaluation, move, teamType, team);
        await db.SaveChangesAsync(ct);

        // The two foreign keys form a cycle, so the scoring model points back at its evaluation in a
        // second save, as EvaluationService.CreateAsync does.
        scoringModel.EvaluationId = evaluation.Id;
        await db.SaveChangesAsync(ct);

        return new EvaluationGraph(scoringModel, category, option, evaluation, move, teamType, team);
    }

    /// <summary>
    /// Seeds a submission of <paramref name="graph"/>'s evaluation with one category and one option per
    /// scoring category and option, as <c>SubmissionService.CreateNewSubmission</c> builds them: a user's
    /// (<paramref name="userId"/> and <paramref name="teamId"/>), a team's (<paramref name="teamId"/>) or
    /// the official one (neither).
    /// </summary>
    public static async Task<SubmissionGraph> SeedSubmissionAsync(
        CiteContext db,
        CancellationToken ct,
        EvaluationGraph graph,
        Guid? teamId = null,
        Guid? userId = null,
        int moveNumber = 0)
    {
        var submission = TestData.Submission(graph.ScoringModel.Id, graph.Evaluation.Id, teamId, userId, moveNumber);
        var category = TestData.SubmissionCategory(submission.Id, graph.Category.Id);
        var option = TestData.SubmissionOption(category.Id, graph.Option.Id);

        db.AddRange(submission, category, option);
        await db.SaveChangesAsync(ct);

        return new SubmissionGraph(submission, category, option);
    }
}
