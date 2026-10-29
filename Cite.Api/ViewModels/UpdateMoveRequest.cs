// Copyright 2026 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.

using System;

namespace Cite.Api.ViewModels
{
    /// <summary>
    /// Fields editable on an existing move. It has no EvaluationId, so an edit cannot move the move to another evaluation.
    /// </summary>
    public class UpdateMoveRequest
    {
        public string Description { get; set; }
        public int MoveNumber { get; set; }
        public DateTime SituationTime { get; set; }
        public string SituationDescription { get; set; }
    }
}
