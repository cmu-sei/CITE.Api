// Copyright 2026 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.

namespace Cite.Api.ViewModels
{
    /// <summary>
    /// The only field editable on an existing submission comment. It has no SubmissionOptionId, so a
    /// request cannot move the comment to another option or submission.
    /// </summary>
    public class UpdateSubmissionCommentRequest
    {
        public string Comment { get; set; }
    }
}
