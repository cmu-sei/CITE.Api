// Copyright 2026 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using Cite.Api.Data.Models;
using Cite.Api.ViewModels;
using Riok.Mapperly.Abstractions;

namespace Cite.Api.Infrastructure.Mappings;

/// <summary>Duties' mappings.</summary>
[Mapper]
[UseStaticMapper(typeof(EvaluationMapper))]
[UseStaticMapper(typeof(TeamMapper))]
[UseStaticMapper(typeof(UserMapper))]
public static partial class DutyMapper
{
    /// <remarks>
    /// <see cref="Duty.Users"/> are the users of the duty's loaded assignments, one per assignment (null for
    /// an assignment whose user is not loaded). The evaluation and team are read from whatever the context
    /// has loaded.
    /// </remarks>
    [MapProperty(nameof(DutyEntity.DutyUsers), nameof(Duty.Users), Use = nameof(MapUsers))]
    public static partial Duty ToDuty(this DutyEntity source);

    /// <remarks>
    /// The duty's users are assigned through their own endpoints, and its evaluation and team are named by
    /// id, so none of them is mapped from the body.
    /// </remarks>
    [MapperRequiredMapping(RequiredMappingStrategy.Source)]
    [MapperIgnoreSource(nameof(Duty.Users))]
    [MapperIgnoreSource(nameof(Duty.Evaluation))]
    [MapperIgnoreSource(nameof(Duty.Team))]
    public static partial DutyEntity ToEntity(this Duty source);

    [UserMapping(Default = false)]
    private static ICollection<User> MapUsers(ICollection<DutyUserEntity> source) =>
        source == null ? [] : source.Select(x => x.User?.ToUser()).ToList();
}
