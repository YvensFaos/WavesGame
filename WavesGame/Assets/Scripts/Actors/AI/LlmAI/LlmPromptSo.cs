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
    [CreateAssetMenu(fileName = "LlmPrompt", menuName = "Waves/LLM/Prompt")]
    public class LlmPromptSo : ScriptableObject
    {
        [TextArea(10, 40)] public string prompt;

        public bool includeEmptySpaces;
    }
}