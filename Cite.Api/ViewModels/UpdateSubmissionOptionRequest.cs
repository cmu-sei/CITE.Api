// Copyright 2026 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.

using System.ComponentModel.DataAnnotations;

namespace Cite.Api.ViewModels
{
    /// <summary>
    /// The only field editable on an existing submission option. It is applied as a selection, so the category's exclusivity and the submission's score are kept, and is required, so a request that leaves it out cannot clear the category.
    /// </summary>
    public class UpdateSubmissionOptionRequest
    {
        [Required]
        public bool? IsSelected { get; set; }
    }
}
