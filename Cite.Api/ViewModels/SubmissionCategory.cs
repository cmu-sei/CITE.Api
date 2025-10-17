// Copyright 2022 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license, please see LICENSE.md in the project root for license information or contact permission@sei.cmu.edu for full terms.

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cite.Api.ViewModels
{
    public class SubmissionCategory : Base, IAuthorizationType
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public Guid Id { get; set; }
        public double Score { get; set; }
        public Guid SubmissionId { get; set; }
        public Guid ScoringCategoryId { get; set; }
        public virtual ICollection<SubmissionOption> SubmissionOptions { get; set; } = new HashSet<SubmissionOption>();
    }

}
