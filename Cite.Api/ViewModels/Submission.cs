// Copyright 2022 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license, please see LICENSE.md in the project root for license information or contact permission@sei.cmu.edu for full terms.

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Cite.Api.Data.Enumerations;

namespace Cite.Api.ViewModels
{
    public class Submission : Base
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public Guid Id { get; set; }
        public double Score { get; set; }
        public ItemStatus Status { get; set; }
        public Guid ScoringModelId { get; set; }
        public Guid? UserId { get; set; }
        public Guid EvaluationId { get; set; }
        public Guid? TeamId { get; set; }
        public Guid? GroupId { get; set; }
        public int MoveNumber { get; set; }
        public bool ScoreIsAnAverage { get; set; }
        public virtual ICollection<SubmissionCategory> SubmissionCategories { get; set; } = new HashSet<SubmissionCategory>();
    }
}

