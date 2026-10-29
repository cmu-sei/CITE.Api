// Copyright 2026 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.

using System;
using Cite.Api.Data.Enumerations;

namespace Cite.Api.ViewModels
{
    /// <summary>
    /// Fields editable on an existing evaluation. It has no ScoringModelId, so an edit cannot point the
    /// evaluation at another evaluation's scoring model, and no teams, moves or submissions.
    /// </summary>
    public class UpdateEvaluationRequest
    {
        public string Description { get; set; }
        public ItemStatus Status { get; set; }
        public int CurrentMoveNumber { get; set; }
        public DateTime SituationTime { get; set; }
        public string SituationDescription { get; set; }
        public Guid? GalleryExhibitId { get; set; }
        public bool ShowAdvanceButton { get; set; }
    }
}
