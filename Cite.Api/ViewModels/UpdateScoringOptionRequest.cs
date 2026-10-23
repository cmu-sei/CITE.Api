// Copyright 2026 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.

namespace Cite.Api.ViewModels
{
    /// <summary>
    /// Fields editable on an existing scoring option. It has no ScoringCategoryId, so an edit cannot move the option to another category.
    /// </summary>
    public class UpdateScoringOptionRequest
    {
        public int DisplayOrder { get; set; }
        public string Description { get; set; }
        public bool IsModifier { get; set; }
        public double Value { get; set; }
    }
}
