/*
 * Copyright (c) 2026 Yvens R Serpa [https://github.com/YvensFaos/]
 *
 * This work is licensed under the MIT License.
 * To view a copy of this license, visit https://opensource.org/licenses/MIT
 * or see the LICENSE file in the root directory of this repository.
 */

using System;
using Newtonsoft.Json;
using UnityEngine;

namespace Core.Recorder
{
    [Serializable]
    public class SimpleVector2Int
    {
        [SerializeField, JsonProperty(DefaultValueHandling = DefaultValueHandling.Include)]
        public int x;

        [SerializeField, JsonProperty(DefaultValueHandling = DefaultValueHandling.Include)]
        public int y;

        public SimpleVector2Int(int x, int y)
        {
            this.x = x;
            this.y = y;
        }

        public SimpleVector2Int(Vector2Int vector)
        {
            x = vector.x;
            y = vector.y;
        }
    }
}