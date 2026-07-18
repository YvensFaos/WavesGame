/*
 * Copyright (c) 2026 Yvens R Serpa [https://github.com/YvensFaos/]
 *
 * This work is licensed under the Creative Commons Attribution 4.0 International License.
 * To view a copy of this license, visit http://creativecommons.org/licenses/by/4.0/
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