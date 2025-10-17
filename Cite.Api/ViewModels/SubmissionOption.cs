// Copyright 2022 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license, please see LICENSE.md in the project root for license information or contact permission@sei.cmu.edu for full terms.

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cite.Api.ViewModels
{
    public class SubmissionOption : Base, IAuthorizationType
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public Guid Id { get; set; }
        public bool IsSelected { get; set; }
        public int SelectedCount { get; set; }
        public Guid SubmissionCategoryId { get; set; }
        public Guid ScoringOptionId { get; set; }
        public virtual ICollection<SubmissionComment> SubmissionComments { get; set; } = new HashSet<SubmissionComment>();
    }

}
