// Copyright 2026 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using Cite.Api.Data.Models;
using Cite.Api.ViewModels;
using Riok.Mapperly.Abstractions;

namespace Cite.Api.Infrastructure.Mappings;

/// <summary>Evaluations' mappings. Missing team, move and submission lists are empty ones in both directions.</summary>
[Mapper]
[UseStaticMapper(typeof(TeamMapper))]
[UseStaticMapper(typeof(MoveMapper))]
[UseStaticMapper(typeof(SubmissionMapper))]
[UseStaticMapper(typeof(ScoringModelMapper))]
public static partial class EvaluationMapper
{
    /// <remarks>
    /// The scoring model, teams, moves and submissions are read from whatever the context has loaded.
    /// <see cref="Evaluation.EvaluationPermissions"/> describes the caller; the controller fills it.
    /// </remarks>
    [MapperIgnoreTarget(nameof(Evaluation.EvaluationPermissions))]
    public static partial Evaluation ToEvaluation(this EvaluationEntity source);

    /// <remarks>
    /// The teams and moves in the body are not mapped. A scoring model and submissions nested in the body
    /// are, and are stored with the evaluation as sent; a nested scoring model replaces the one
    /// <see cref="Evaluation.ScoringModelId"/> names.
    /// </remarks>
    [MapperRequiredMapping(RequiredMappingStrategy.Source)]
    [MapperIgnoreSource(nameof(Evaluation.Teams))]
    [MapperIgnoreSource(nameof(Evaluation.Moves))]
    [MapperIgnoreSource(nameof(Evaluation.EvaluationPermissions))]
    public static partial EvaluationEntity ToEntity(this Evaluation source);

    /// <remarks>Writes every member of the request, so an exhibit or a situation the body leaves out is cleared.</remarks>
    [MapperRequiredMapping(RequiredMappingStrategy.Source)]
    public static partial void ApplyTo(this UpdateEvaluationRequest source, EvaluationEntity target);

    private static ICollection<Team> MapTeams(ICollection<TeamEntity> source) =>
        source == null ? [] : source.Select(x => x.ToTeam()).ToList();

    private static ICollection<Move> MapMoves(ICollection<MoveEntity> source) =>
        source == null ? [] : source.Select(x => x.ToMove()).ToList();

    private static ICollection<Submission> MapSubmissions(ICollection<SubmissionEntity> source) =>
        source == null ? [] : source.Select(x => x.ToSubmission()).ToList();

    private static ICollection<SubmissionEntity> MapSubmissions(ICollection<Submission> source) =>
        source == null ? [] : source.Select(x => x.ToEntity()).ToList();
}
