/*
 * Copyright (c) 2026 Yvens R Serpa [https://github.com/YvensFaos/]
 *
 * This work is licensed under the Creative Commons Attribution 4.0 International License.
 * To view a copy of this license, visit http://creativecommons.org/licenses/by/4.0/
 * or see the LICENSE file in the root directory of this repository.
 */

using UnityEngine;

namespace Actors.AI.LlmAI
{
    [CreateAssetMenu(fileName = "Llm Model Pair", menuName = "Waves/LLM/Model Pair", order = 2)]
    public class LlmModelPairSo : ScriptableObject
    {
        public LlmModelPair modelPair;

        public override string ToString()
        {
            return modelPair.ToString();
        }
    }
}