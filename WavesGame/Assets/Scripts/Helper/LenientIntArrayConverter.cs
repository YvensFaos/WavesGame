/*
 * Copyright (c) 2026 Yvens R Serpa [https://github.com/YvensFaos/]
 *
 * This work is licensed under the Creative Commons Attribution 4.0 International License.
 * To view a copy of this license, visit http://creativecommons.org/licenses/by/4.0/
 * or see the LICENSE file in the root directory of this repository.
 */

using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Helper
{
    public sealed class LenientIntArrayConverter : JsonConverter<int[]>
    {
        public override int[] ReadJson(JsonReader reader, Type objectType,
            int[] existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
                return null;

            if (reader.TokenType != JsonToken.StartArray)
                throw new JsonSerializationException(
                    $"Expected array for int[], got {reader.TokenType}.");

            var result = new List<int>();
            while (reader.Read())
            {
                switch (reader.TokenType)
                {
                    case JsonToken.EndArray:
                        return result.ToArray();
                    case JsonToken.Null:
                        return null;
                    case JsonToken.None:
                    case JsonToken.StartObject:
                    case JsonToken.StartArray:
                    case JsonToken.StartConstructor:
                    case JsonToken.PropertyName:
                    case JsonToken.Comment:
                    case JsonToken.Raw:
                    case JsonToken.Integer:
                    case JsonToken.Float:
                    case JsonToken.String:
                    case JsonToken.Boolean:
                    case JsonToken.Undefined:
                    case JsonToken.EndObject:
                    case JsonToken.EndConstructor:
                    case JsonToken.Date:
                    case JsonToken.Bytes:
                    default:
                        result.Add(Convert.ToInt32(reader.Value));
                        break;
                }
            }

            throw new JsonSerializationException("Unexpected end of JSON in int[].");
        }

        public override void WriteJson(JsonWriter writer, int[] value,
            JsonSerializer serializer)
        {
            if (value == null)
            {
                writer.WriteNull();
                return;
            }

            writer.WriteStartArray();
            foreach (var v in value) writer.WriteValue(v);
            writer.WriteEndArray();
        }
    }
}