// Copyright 2026 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.

using System;
using System.Net;

namespace Cite.Api.Infrastructure.Exceptions
{
    /// <summary>
    /// A request names a record that does not belong to the parent the caller is authorized on,
    /// or would break a uniqueness rule.
    /// </summary>
    public class ConflictException : Exception, IApiException
    {
        public ConflictException()
            : base("Conflict")
        {
        }

        public ConflictException(string message)
            : base(message)
        {
        }

        public HttpStatusCode GetStatusCode()
        {
            return HttpStatusCode.Conflict;
        }
    }
}
