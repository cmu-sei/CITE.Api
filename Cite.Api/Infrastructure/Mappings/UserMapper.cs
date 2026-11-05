// Copyright 2026 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using Cite.Api.Data.Models;
using Cite.Api.ViewModels;
using Riok.Mapperly.Abstractions;

namespace Cite.Api.Infrastructure.Mappings;

/// <summary>Users' mappings.</summary>
/// <remarks>
/// <see cref="User.RoleId"/> is a string: it is written with <c>Guid.ToString()</c> and read with
/// <c>Guid.Parse</c>, so any GUID format is accepted, no role id is no role, and anything else (the empty
/// string included) throws, which is answered with a 500.
/// </remarks>
[Mapper]
[UseStaticMapper(typeof(SubmissionMapper))]
public static partial class UserMapper
{
    /// <remarks><see cref="User.Submissions"/> holds whatever submissions the context has loaded.</remarks>
    public static partial User ToUser(this UserEntity source);

    /// <remarks>Every member but the submissions is written, the audit fields included; <c>CiteContext</c> adjusts those as it saves.</remarks>
    [MapperRequiredMapping(RequiredMappingStrategy.Source)]
    [MapperIgnoreSource(nameof(User.Submissions))]
    public static partial UserEntity ToEntity(this User source);

    /// <remarks>
    /// Writes the body's id onto the row as well, so a body whose id is not the route's (or has none)
    /// tries to change the key and fails the save. A body without a role id takes the role away.
    /// </remarks>
    [MapperRequiredMapping(RequiredMappingStrategy.Source)]
    [MapperIgnoreSource(nameof(User.Submissions))]
    public static partial void ApplyTo(this User source, UserEntity target);

    private static ICollection<Submission> MapSubmissions(ICollection<SubmissionEntity> source) =>
        source == null ? [] : source.Select(x => x.ToSubmission()).ToList();
}
