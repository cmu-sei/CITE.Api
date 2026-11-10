// Copyright 2026 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using Cite.Api.Data.Models;
using Cite.Api.ViewModels;
using Riok.Mapperly.Abstractions;

namespace Cite.Api.Infrastructure.Mappings;

/// <summary>
/// Scoring models' and their severity levels' mappings. Missing category and severity level lists are
/// empty ones in both directions; an update that leaves the severity levels out keeps the saved ones.
/// </summary>
[Mapper]
[UseStaticMapper(typeof(ScoringCategoryMapper))]
public static partial class ScoringModelMapper
{
    /// <remarks><see cref="ScoringModel.ScoringModelPermissions"/> describes the caller; the controller fills it.</remarks>
    [MapperIgnoreTarget(nameof(ScoringModel.ScoringModelPermissions))]
    public static partial ScoringModel ToScoringModel(this ScoringModelEntity source);

    /// <remarks>
    /// Only an evaluation's create body nests a whole scoring model; it is stored as sent, with its
    /// categories and their options.
    /// </remarks>
    [MapperRequiredMapping(RequiredMappingStrategy.Source)]
    [MapperIgnoreSource(nameof(ScoringModel.ScoringModelPermissions))]
    public static partial ScoringModelEntity ToEntity(this ScoringModel source);

    /// <remarks>
    /// <c>ScoringModelService</c> sets the id and builds the categories and options as new rows, so neither
    /// is mapped.
    /// </remarks>
    [MapperRequiredMapping(RequiredMappingStrategy.Source)]
    [MapperIgnoreSource(nameof(CreateScoringModelRequest.Id))]
    [MapperIgnoreSource(nameof(CreateScoringModelRequest.ScoringCategories))]
    public static partial ScoringModelEntity ToEntity(this CreateScoringModelRequest source);

    /// <summary>
    /// Applies the update onto the stored scoring model: every member is written, so a string the body
    /// leaves out is cleared, but severity levels left out keep the saved ones.
    /// </summary>
    public static void ApplyTo(this UpdateScoringModelRequest source, ScoringModelEntity target)
    {
        ApplyFieldsTo(source, target);
        if (source.SeverityLevels != null)
            target.SeverityLevels = MapSeverityLevels(source.SeverityLevels);
    }

    [MapperRequiredMapping(RequiredMappingStrategy.Source)]
    [MapperIgnoreSource(nameof(UpdateScoringModelRequest.SeverityLevels))]
    private static partial void ApplyFieldsTo(UpdateScoringModelRequest source, ScoringModelEntity target);

    public static partial SeverityLevel ToSeverityLevel(this SeverityLevelEntity source);

    [MapperRequiredMapping(RequiredMappingStrategy.Source)]
    public static partial SeverityLevelEntity ToEntity(this SeverityLevel source);

    private static List<SeverityLevel> MapSeverityLevels(List<SeverityLevelEntity> source) =>
        source == null ? [] : source.Select(x => x.ToSeverityLevel()).ToList();

    private static List<SeverityLevelEntity> MapSeverityLevels(List<SeverityLevel> source) =>
        source == null ? [] : source.Select(x => x.ToEntity()).ToList();

    private static ICollection<ScoringCategory> MapCategories(ICollection<ScoringCategoryEntity> source) =>
        source == null ? [] : source.Select(x => x.ToScoringCategory()).ToList();

    private static ICollection<ScoringCategoryEntity> MapCategories(ICollection<ScoringCategory> source) =>
        source == null ? [] : source.Select(x => x.ToEntity()).ToList();
}
