// Copyright 2026 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.

using System;

namespace Cite.Api.ViewModels
{
    /// <summary>
    /// The fields a team's create and edit share. The evaluation is not one of them: only
    /// <see cref="CreateTeamRequest"/> names it, so an edit cannot move a team to another evaluation. The team
    /// type is named by id only, so a request cannot create a team type.
    /// </summary>
    public abstract class TeamFields
    {
        public string Name { get; set; }
        public string ShortName { get; set; }
        public Guid TeamTypeId { get; set; }
        public bool HideScoresheet { get; set; }
    }

    public class CreateTeamRequest : TeamFields
    {
        public Guid Id { get; set; }
        public Guid? EvaluationId { get; set; }
    }

    public class UpdateTeamRequest : TeamFields
    {
    }
}
