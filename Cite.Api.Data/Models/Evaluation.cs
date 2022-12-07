// Copyright 2022 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license, please see LICENSE.md in the project root for license information or contact permission@sei.cmu.edu for full terms.

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Cite.Api.Data.Enumerations;

namespace Cite.Api.Data.Models
{
    public class EvaluationEntity : BaseEntity
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
        public virtual ScoringModelEntity ScoringModel { get; set; }
        public Guid? GalleryExhibitId { get; set; }
        public virtual ICollection<TeamEntity> Teams { get; set; } = new HashSet<TeamEntity>();
        public virtual ICollection<MoveEntity> Moves { get; set; } = new HashSet<MoveEntity>();
        public ICollection<SubmissionEntity> Submissions { get; set; } = new List<SubmissionEntity>();
    }
}

