// Copyright 2026 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.

using Cite.Api.Data.Models;
using Riok.Mapperly.Abstractions;
using ActionModel = Cite.Api.ViewModels.Action;

namespace Cite.Api.Infrastructure.Mappings;

/// <summary>Actions' mappings.</summary>
[Mapper]
[UseStaticMapper(typeof(EvaluationMapper))]
[UseStaticMapper(typeof(TeamMapper))]
public static partial class ActionMapper
{
    /// <remarks>The evaluation and team are read from whatever the context has loaded.</remarks>
    public static partial ActionModel ToAction(this ActionEntity source);

    /// <remarks>The evaluation and team are named by id, so a nested evaluation or team in the body is not mapped.</remarks>
    [MapperRequiredMapping(RequiredMappingStrategy.Source)]
    [MapperIgnoreSource(nameof(ActionModel.Evaluation))]
    [MapperIgnoreSource(nameof(ActionModel.Team))]
    public static partial ActionEntity ToEntity(this ActionModel source);
}
