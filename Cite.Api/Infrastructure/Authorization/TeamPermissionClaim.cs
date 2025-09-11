// Copyright 2025 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.

using System;
using System.Text.Json;
using Cite.Api.Data.Enumerations;

namespace Cite.Api.Infrastructure.Authorization;

public class TeamPermissionClaim
{
    public Guid TeamId { get; set; }
    public TeamPermission[] Permissions { get; set; } = [];

    public TeamPermissionClaim() { }

    public static TeamPermissionClaim FromString(string json)
    {
        return JsonSerializer.Deserialize<TeamPermissionClaim>(json);
    }

    public override string ToString()
    {
        return JsonSerializer.Serialize(this);
    }
}
