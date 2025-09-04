// Copyright 2024 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.

using System;
using System.Text.Json;
using Cite.Api.Data;

namespace Cite.Api.Infrastructure.Authorization;

public class EvaluationPermissionClaim
{
    public Guid EvaluationId { get; set; }
    public EvaluationPermission[] Permissions { get; set; } = [];

    public EvaluationPermissionClaim() { }

    public static EvaluationPermissionClaim FromString(string json)
    {
        return JsonSerializer.Deserialize<EvaluationPermissionClaim>(json);
    }

    public override string ToString()
    {
        return JsonSerializer.Serialize(this);
    }
}
