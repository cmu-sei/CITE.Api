// Copyright 2022 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license, please see LICENSE.md in the project root for license information or contact permission@sei.cmu.edu for full terms.

using System;

namespace Cite.Api.Infrastructure.QueryParameters
{
    public class ScoringCategoryGet
    {
        /// <summary>
        /// Whether or not to return records only for descriptions containing the designated string
        /// </summary>
        public string Description { get; set; }

    }
}

