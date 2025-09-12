// Copyright 2022 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license, please see LICENSE.md in the project root for license information or contact permission@sei.cmu.edu for full terms.

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Cite.Api.Data.Enumerations;

namespace Cite.Api.ViewModels
{
    public class ScoringCategory : Base, IAuthorizationType
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public Guid Id { get; set; }
        public int DisplayOrder { get; set; }
        public string Description { get; set; }
        public string CalculationEquation { get; set; }
        public bool IsModifierRequired { get; set; }
        public double ScoringWeight { get; set; }
        public int MoveNumberFirstDisplay { get; set; }
        public int MoveNumberLastDisplay { get; set; }
        public Guid ScoringModelId { get; set; }
        public virtual ICollection<ScoringOption> ScoringOptions { get; set; } = new HashSet<ScoringOption>();
        public ScoringOptionSelection ScoringOptionSelection { get; set; }
    }

}
