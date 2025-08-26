// Copyright 2022 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license, please see LICENSE.md in the project root for license information or contact permission@sei.cmu.edu for full terms.

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Cite.Api.Data.Enumerations;

namespace Cite.Api.Data.Models
{
    public class ScoringModelEntity : BaseEntity
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public Guid Id { get; set; }
        public string Description { get; set; }
        public string CalculationEquation { get; set; }
        public ItemStatus Status { get; set; }
        public virtual ICollection<ScoringCategoryEntity> ScoringCategories { get; set; } = new HashSet<ScoringCategoryEntity>();
        public bool HideScoresOnScoreSheet { get; set; }
        public bool DisplayCommentTextBoxes { get; set; }
        public bool DisplayScoringModelByMoveNumber { get; set; }
        public bool ShowPastSituationDescriptions { get; set; }
        public bool UseSubmit { get; set; }
        public bool UseUserScore { get; set; }
        public bool UseTeamScore { get; set; }
        public bool UseTeamAverageScore { get; set; }
        public bool UseTypeAverageScore { get; set; }
        public bool UseOfficialScore { get; set; }
        public RightSideDisplay RightSideDisplay { get; set; }
        public string RightSideHtmlBlock { get; set; }
        public string RightSideEmbeddedUrl { get; set; }
        public Guid? EvaluationId { get; set; }
    }
}
