// Copyright 2022 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license, please see LICENSE.md in the project root for license information or contact permission@sei.cmu.edu for full terms.

using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cite.Api.Data.Models
{
    public class ActionEntity : BaseEntity
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public Guid Id { get; set; }
        public Guid EvaluationId { get; set; }
        public virtual EvaluationEntity Evaluation { get; set; }
        public Guid TeamId { get; set; }
        public virtual TeamEntity Team { get; set; }
        public int MoveNumber { get; set; }
        public int InjectNumber { get; set; }
        public int ActionNumber { get; set; }
        public string Description { get; set; }
        public bool IsChecked { get; set; }
        public Guid? ChangedBy { get; set; }
    }

}

