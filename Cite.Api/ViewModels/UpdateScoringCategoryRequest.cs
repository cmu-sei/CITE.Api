// Copyright 2026 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.

using Cite.Api.Data.Enumerations;

namespace Cite.Api.ViewModels
{
    /// <summary>
    /// Fields editable on an existing scoring category. It has no ScoringModelId and no options, so an edit cannot move the category or rewrite options.
    /// </summary>
    public class UpdateScoringCategoryRequest
    {
        public int DisplayOrder { get; set; }
        public string Description { get; set; }
        public string CalculationEquation { get; set; }
        public bool IsModifierRequired { get; set; }
        public double ScoringWeight { get; set; }
        public int MoveNumberFirstDisplay { get; set; }
        public int MoveNumberLastDisplay { get; set; }
        public ScoringOptionSelection ScoringOptionSelection { get; set; }
    }
}
