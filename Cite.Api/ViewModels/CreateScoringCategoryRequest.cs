// Copyright 2026 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Cite.Api.Data.Enumerations;

namespace Cite.Api.ViewModels
{
    /// <summary>
    /// A category created with its scoring model. It has no ID and no ScoringModelId: it is a new row of the model
    /// it is sent in, and the server gives it its ID.
    /// </summary>
    public class CreateScoringCategoryRequest : IValidatableObject
    {
        public int DisplayOrder { get; set; }
        public string Description { get; set; }
        public string CalculationEquation { get; set; }
        public bool IsModifierRequired { get; set; }
        public double ScoringWeight { get; set; }
        public int MoveNumberFirstDisplay { get; set; }
        public int MoveNumberLastDisplay { get; set; }
        public ScoringOptionSelection ScoringOptionSelection { get; set; }
        public List<CreateScoringOptionRequest> ScoringOptions { get; set; } = [];

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (!Enum.IsDefined(ScoringOptionSelection))
                yield return new ValidationResult($"{ScoringOptionSelection} is not a scoring option selection.", [nameof(ScoringOptionSelection)]);
            if (MoveNumberFirstDisplay > MoveNumberLastDisplay)
                yield return new ValidationResult("The first move a category is shown in cannot be after the last.", [nameof(MoveNumberFirstDisplay)]);
        }
    }

    /// <summary>
    /// An option created with its category. It has no ID and no ScoringCategoryId: it is a new row of the category
    /// it is sent in, and the server gives it its ID.
    /// </summary>
    public class CreateScoringOptionRequest
    {
        public int DisplayOrder { get; set; }
        public string Description { get; set; }
        public bool IsModifier { get; set; }
        public double Value { get; set; }
    }
}
