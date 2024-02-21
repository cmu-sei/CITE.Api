// Copyright 2022 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license, please see LICENSE.md in the project root for license information or contact permission@sei.cmu.edu for full terms.

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Cite.Api.Data.Enumerations;

namespace Cite.Api.ViewModels
{
    public class ScoringModel : Base
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public Guid Id { get; set; }
        public string Description { get; set; }
        public string CalculationEquation { get; set; }
        public ItemStatus Status { get; set; }
        public virtual ICollection<ScoringCategory> ScoringCategories { get; set; } = new HashSet<ScoringCategory>();
        public bool HideScoresOnScoreSheet { get; set; }
        public bool DisplayCommentTextBoxes { get; set; }
        public bool DisplayScoringModelByMoveNumber { get; set; }
    }
}

