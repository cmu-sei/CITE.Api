// Copyright 2026 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.

namespace Cite.Api.ViewModels
{
    /// <summary>
    /// The only field editable on an existing submission option. It is applied as a selection, so the category's exclusivity and the submission's score are kept.
    /// </summary>
    public class UpdateSubmissionOptionRequest
    {
        public bool IsSelected { get; set; }
    }
}
