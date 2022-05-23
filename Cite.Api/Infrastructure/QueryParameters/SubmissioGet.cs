// Copyright 2022 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license, please see LICENSE.md in the project root for license information or contact permission@sei.cmu.edu for full terms.

using System;

namespace Cite.Api.Infrastructure.QueryParameters
{
    public class SubmissionGet
    {
        /// <summary>
        /// Whether or not to return records only for a designated user
        /// </summary>
        public string EvaluationId { get; set; }

        /// <summary>
        /// Whether or not to return records only for a designated user
        /// </summary>
        public string ScoringModelId { get; set; }

        /// <summary>
        /// Whether or not to return records only for a designated user
        /// </summary>
        public string UserId { get; set; }

        /// <summary>
        /// Whether or not to return records only for a designated team
        /// </summary>
        public string TeamId { get; set; }

    }
}

