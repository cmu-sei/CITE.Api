// Copyright 2021 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.

using System;
using Cite.Api.Data.Enumerations;

namespace Cite.Api.ViewModels
{
    public class ScenarioRole
    {

        public Guid Id { get; set; }

        public string Name { get; set; }
        public bool AllPermissions { get; set; }

        public EvaluationPermission[] Permissions { get; set; }
    }
}
