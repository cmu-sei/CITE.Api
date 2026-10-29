// Copyright 2026 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.

using System.ComponentModel.DataAnnotations;

namespace Cite.Api.ViewModels
{
    public class SeverityLevel
    {
        [Required(AllowEmptyStrings = false)]
        public string Name { get; set; }
        public double MinimumScore { get; set; }
        [Required]
        [RegularExpression(SeverityLevelRules.ColorPattern)]
        public string Color { get; set; }
        public string Description { get; set; }
    }
}
