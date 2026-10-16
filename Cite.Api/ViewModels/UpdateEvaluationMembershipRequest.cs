// Copyright 2026 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.

using System;

namespace Cite.Api.ViewModels
{
    /// <summary>
    /// Fields editable on an existing evaluation membership. It has no EvaluationId, so a request
    /// cannot name a different evaluation than the one the membership belongs to.
    /// </summary>
    public class UpdateEvaluationMembershipRequest
    {
        public Guid RoleId { get; set; }
    }
}
