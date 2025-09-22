// Copyright 2022 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license, please see LICENSE.md in the project root for license information or contact permission@sei.cmu.edu for full terms.

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cite.Api.Data.Models
{
    public class DutyEntity : BaseEntity
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public Guid Id { get; set; }
        public Guid EvaluationId { get; set; }
        public virtual EvaluationEntity Evaluation { get; set; }
        public Guid TeamId { get; set; }
        public virtual TeamEntity Team { get; set; }
        public string Name { get; set; }
        public ICollection<DutyUserEntity> DutyUsers { get; set; } = new HashSet<DutyUserEntity>();
    }

}
