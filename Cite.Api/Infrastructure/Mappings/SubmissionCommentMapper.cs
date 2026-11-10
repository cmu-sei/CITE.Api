// Copyright 2026 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.

using Cite.Api.Data.Models;
using Cite.Api.ViewModels;
using Riok.Mapperly.Abstractions;

namespace Cite.Api.Infrastructure.Mappings;

/// <summary>Submission comments' mappings.</summary>
[Mapper]
public static partial class SubmissionCommentMapper
{
    public static partial SubmissionComment ToSubmissionComment(this SubmissionCommentEntity source);

    [MapperRequiredMapping(RequiredMappingStrategy.Source)]
    public static partial SubmissionCommentEntity ToEntity(this SubmissionComment source);
}
