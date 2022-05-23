// Copyright 2022 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license, please see LICENSE.md in the project root for license information or contact permission@sei.cmu.edu for full terms.

using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cite.Api.ViewModels
{
    public class EvaluationTeam : Base
    {
        public EvaluationTeam() { }

        public EvaluationTeam(Guid teamId, Guid evaluationId)
        {
            TeamId = teamId;
            EvaluationId = evaluationId;
        }

        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public Guid Id { get; set; }

        public Guid TeamId { get; set; }
        public Team Team { get; set; }

        public Guid EvaluationId { get; set; }
        public Evaluation Evaluation { get; set; }
    }

}

