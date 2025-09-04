// Copyright 2024 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.

using System;
using System.Text.Json;
using Cite.Api.Data;

namespace Cite.Api.Infrastructure.Authorization;

public class ScoringModelPermissionClaim
{
    public Guid ScoringModelId { get; set; }
    public ScoringModelPermission[] Permissions { get; set; } = [];

    public ScoringModelPermissionClaim() { }

    public static ScoringModelPermissionClaim FromString(string json)
    {
        return JsonSerializer.Deserialize<ScoringModelPermissionClaim>(json);
    }

    public override string ToString()
    {
        return JsonSerializer.Serialize(this);
    }
}
