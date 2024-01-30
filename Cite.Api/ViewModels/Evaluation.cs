// Copyright 2022 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license, please see LICENSE.md in the project root for license information or contact permission@sei.cmu.edu for full terms.

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Cite.Api.Data.Enumerations;

namespace Cite.Api.ViewModels
{
    public class Evaluation : Base
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
         public Guid Id { get; set; }
        public string Description { get; set; }
        public ItemStatus Status { get; set; }
        public int CurrentMoveNumber { get; set;}
        public DateTime SituationTime { get; set;}
        public string SituationDescription { get; set; }
        public Guid ScoringModelId { get; set; }
        public virtual ScoringModel ScoringModel { get; set; }
        public Guid? GalleryExhibitId { get; set; }
        public bool HideScoresOnScoreSheet { get; set; }
        public bool ShowPastSituationDescriptions { get; set; }
        public bool DisplayCommentTextBoxes { get; set; }
        public RightSideDisplay RightSideDisplay { get; set; }
        public string RightSideHtmlBlock { get; set; }
        public string RightSideEmbeddedUrl { get; set; }
        public virtual ICollection<Team> Teams { get; set; } = new HashSet<Team>();
        public virtual ICollection<Move> Moves { get; set; } = new HashSet<Move>();
        public virtual ICollection<Submission> Submissions { get; set; } = new List<Submission>();
   }
}

