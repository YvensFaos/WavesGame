/*
 * Copyright (c) 2026 Yvens R Serpa [https://github.com/YvensFaos/]
 *
 * This work is licensed under the MIT License.
 * To view a copy of this license, visit https://opensource.org/licenses/MIT
 * or see the LICENSE file in the root directory of this repository.
 */

using System;
using FALLA;
using UUtils;

namespace Actors.AI.LlmAI
{
    [Serializable]
    public class LlmModelPair : Pair<LlmType, string>
    {
        public LlmModelPair(LlmType one, string two) : base(one, two)
        {
        }

        public override string ToString()
        {
            return $"{One}-{(string.IsNullOrEmpty(Two) ? "default" : Two)}";
        }
    }
}