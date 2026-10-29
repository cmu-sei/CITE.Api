// Copyright 2026 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Cite.Api.Data.Enumerations;

namespace Cite.Api.ViewModels
{
    /// <summary>
    /// The fields a scoring model's create and edit share. The evaluation that owns a scoring model copy and
    /// the categories are not among them: the server attaches a copy to its evaluation, and categories are
    /// written through their own endpoints.
    /// </summary>
    public abstract class ScoringModelFields : IValidatableObject
    {
        public string Description { get; set; }
        public string CalculationEquation { get; set; }
        public ItemStatus Status { get; set; }
        public bool HideScoresOnScoreSheet { get; set; }
        public bool DisplayCommentTextBoxes { get; set; }
        public bool DisplayScoringModelByMoveNumber { get; set; }
        public bool ShowPastSituationDescriptions { get; set; }
        public bool UseSubmit { get; set; }
        public bool UseUserScore { get; set; }
        public bool UseTeamScore { get; set; }
        public bool UseTeamAverageScore { get; set; }
        public bool UseTypeAverageScore { get; set; }
        public bool UseOfficialScore { get; set; }
        public RightSideDisplay RightSideDisplay { get; set; }
        public string RightSideHtmlBlock { get; set; }
        public string RightSideEmbeddedUrl { get; set; }
        // empty means the default severity scale; left out of an update, the saved levels are kept
        public List<SeverityLevel> SeverityLevels { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (SeverityLevels == null)
                yield break;
            foreach (var problem in SeverityLevelRules.SharedMinimumScoreProblems(SeverityLevels.Select(l => l.MinimumScore)))
                yield return new ValidationResult(problem, [nameof(SeverityLevels)]);
        }
    }

    public class CreateScoringModelRequest : ScoringModelFields
    {
        public Guid Id { get; set; }
    }

    public class UpdateScoringModelRequest : ScoringModelFields
    {
    }
}
