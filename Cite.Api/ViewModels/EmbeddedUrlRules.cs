// Copyright 2026 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.

using System;
using Cite.Api.Data.Models;
using Cite.Api.Infrastructure.Exceptions;

namespace Cite.Api.ViewModels
{
    /// <summary>
    /// The rule a scoring model's right side embedded URL must follow. The UI loads the URL in an iframe, so only
    /// an absolute http or https URL is accepted: a javascript:, data: or other scheme could run script for whoever
    /// views the scoring model. An empty URL is accepted, and clears the field. The API models' validation and
    /// the copies and json uploads, which write the database model from a stored model or a file, all refuse a URL
    /// that breaks the rule with a 400.
    /// </summary>
    public static class EmbeddedUrlRules
    {
        public const string Problem = "the right side embedded URL must be an absolute http or https URL";

        public static bool IsValid(string url)
            => string.IsNullOrEmpty(url)
                || (Uri.TryCreate(url, UriKind.Absolute, out var uri)
                    && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps));

        // how to get past the refusal, for a stored scoring model and for one in an uploaded file
        public const string EditTheScoringModel = "Edit the scoring model to fix or clear the URL.";
        public const string FixTheFile = "Fix or clear the URL in the file.";

        /// <summary>
        /// Refuse a write with a 400 when a scoring model's embedded URL breaks the rule. The message is
        /// "<paramref name="refused"/>: the rule. <paramref name="fix"/>", so it names the scoring model and what
        /// failed; it leaves out the URL itself.
        /// </summary>
        public static void ThrowIfInvalid(string url, string refused, string fix)
        {
            if (!IsValid(url))
                throw new BadRequestException($"{refused}: {Problem}. {fix}");
        }

        /// <summary>The name a refusal gives a scoring model: its description and id.</summary>
        public static string Name(ScoringModelEntity scoringModel)
            => $"scoring model \"{scoringModel.Description}\" ({scoringModel.Id})";
    }
}
