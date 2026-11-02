/*
 * Copyright (c) 2026 Yvens R Serpa [https://github.com/YvensFaos/]
 *
 * This work is licensed under the MIT License.
 * To view a copy of this license, visit https://opensource.org/licenses/MIT
 * or see the LICENSE file in the root directory of this repository.
 */

using System;
using System.Globalization;
using Newtonsoft.Json;

namespace Core.Recorder.Extras
{
    public class FloatRoundingConverter : JsonConverter<float>
    {
        private readonly int _decimals;

        public FloatRoundingConverter() => _decimals = 2;
        public FloatRoundingConverter(int decimals) => _decimals = decimals;

        public override void WriteJson(JsonWriter writer, float value, JsonSerializer serializer)
        {
            var rounded = (float)Math.Round(value, _decimals);
            writer.WriteValue(rounded);
        }

        public override float ReadJson(JsonReader reader, Type objectType, float existingValue, bool hasExistingValue,
            JsonSerializer serializer)
        {
            return reader.Value != null ? Convert.ToSingle(reader.Value, CultureInfo.InvariantCulture) : 0f;
        }
    }
}