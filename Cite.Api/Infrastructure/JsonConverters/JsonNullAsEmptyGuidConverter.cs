// Copyright 2026 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.

using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cite.Api.Infrastructure.JsonConverters
{
    /// <summary>
    /// Reads a JSON null as <see cref="Guid.Empty"/>, for a required ID whose service applies a default when none is given,
    /// so a client that sends null gets the default instead of a 400.
    /// </summary>
    class JsonNullAsEmptyGuidConverter : JsonConverter<Guid>
    {
        public override Guid Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
            {
                return Guid.Empty;
            }
            return reader.GetGuid();
        }

        public override void Write(Utf8JsonWriter writer, Guid value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value);
        }
    }
}
