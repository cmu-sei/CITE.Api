// Copyright 2026 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.

using System.ComponentModel.DataAnnotations;
using Cite.Api.Data.Enumerations;

namespace Cite.Api.ViewModels
{
    /// <summary>
    /// The only field a caller can change on an existing submission. The score, the owner and the
    /// scoresheet are kept by the server, so a request cannot set them or move the submission.
    /// </summary>
    public class UpdateSubmissionRequest
    {
        [Required]
        [EnumDataType(typeof(ItemStatus))]
        public ItemStatus? Status { get; set; }
    }
}
