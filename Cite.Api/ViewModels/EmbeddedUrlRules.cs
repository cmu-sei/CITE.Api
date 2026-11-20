// Copyright 2026 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.

using System;

namespace Cite.Api.ViewModels
{
    /// <summary>
    /// The rule a scoring model's right side embedded URL must follow. The UI loads the URL in an iframe, so only
    /// an absolute http or https URL is accepted: a javascript:, data: or other scheme could run script for whoever
    /// views the scoring model. An empty URL is accepted, and clears the field. The API models refuse a URL that
    /// breaks the rule with a 400; the copies, which write the database model from a stored model or an uploaded
    /// file, drop it instead.
    /// </summary>
    public static class EmbeddedUrlRules
    {
        public const string Problem = "the right side embedded URL must be an absolute http or https URL";

        public static bool IsValid(string url)
            => string.IsNullOrEmpty(url)
                || (Uri.TryCreate(url, UriKind.Absolute, out var uri)
                    && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps));
    }
}
