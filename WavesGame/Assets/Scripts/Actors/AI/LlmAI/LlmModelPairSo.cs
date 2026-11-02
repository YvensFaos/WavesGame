/*
 * Copyright (c) 2026 Yvens R Serpa [https://github.com/YvensFaos/]
 *
 * This work is licensed under the MIT License.
 * To view a copy of this license, visit https://opensource.org/licenses/MIT
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