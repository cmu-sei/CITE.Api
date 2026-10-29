// Copyright 2026 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.

using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Cite.Api.Data.Models;
using Cite.Api.Infrastructure.Exceptions;

namespace Cite.Api.ViewModels
{
    /// <summary>
    /// The rules a scoring model's severity levels must follow, shared by the API model's validation
    /// and the json uploads, which read files straight into the database model.
    /// </summary>
    public static class SeverityLevelRules
    {
        // a #rrggbb color, so the UI can pick a readable text color for it
        public const string ColorPattern = "^#[0-9a-fA-F]{6}$";

        public static IEnumerable<string> SharedMinimumScoreProblems(IEnumerable<double> minimumScores)
            => minimumScores
                .GroupBy(score => score)
                .Where(g => g.Count() > 1)
                .Select(g => $"two severity levels share minimum score {g.Key.ToString(CultureInfo.InvariantCulture)}");

        /// <summary>
        /// Everything wrong with a list of severity levels, naming each level by its position from 1.
        /// </summary>
        public static List<string> Problems(IEnumerable<(string Name, string Color, double MinimumScore)> levels)
        {
            var list = (levels ?? []).ToList();
            var problems = new List<string>();
            for (var i = 0; i < list.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(list[i].Name))
                    problems.Add($"severity level {i + 1} needs a name");
                if (list[i].Color == null || !Regex.IsMatch(list[i].Color, ColorPattern))
                    problems.Add($"'{list[i].Color}' is not a #rrggbb color (severity level {i + 1})");
            }
            problems.AddRange(SharedMinimumScoreProblems(list.Select(l => l.MinimumScore)));
            return problems;
        }

        /// <summary>
        /// Refuse an uploaded file's severity levels with a 400 that lists every problem.
        /// </summary>
        public static void ThrowIfInvalid(IEnumerable<SeverityLevelEntity> levels)
        {
            var problems = Problems((levels ?? []).Select(l => (l.Name, l.Color, l.MinimumScore)));
            if (problems.Count > 0)
                throw new BadRequestException("Invalid severity levels: " + string.Join("; ", problems));
        }
    }
}
