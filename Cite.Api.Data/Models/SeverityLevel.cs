// Copyright 2026 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.

namespace Cite.Api.Data.Models
{
    /// <summary>
    /// One band of a scoring model's severity scale, stored in the scoring model's severity_levels json column.
    /// A score falls in the level with the highest MinimumScore at or below it; a score below
    /// every minimum falls in the lowest level.
    /// </summary>
    public class SeverityLevelEntity
    {
        public string Name { get; set; }
        public double MinimumScore { get; set; }
        public string Color { get; set; }
        public string Description { get; set; }
    }
}
