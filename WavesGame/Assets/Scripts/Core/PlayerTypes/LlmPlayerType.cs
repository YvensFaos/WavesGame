/*
 * Copyright (c) 2026 Yvens R Serpa [https://github.com/YvensFaos/]
 *
 * This work is licensed under the Creative Commons Attribution 4.0 International License.
 * To view a copy of this license, visit http://creativecommons.org/licenses/by/4.0/
 * or see the LICENSE file in the root directory of this repository.
 */

using Actors;
using Actors.AI.LlmAI;
using UnityEngine;
using UUtils;

namespace Core.PlayerTypes
{
    [CreateAssetMenu(fileName = "LLM Player Type", menuName = "Waves/Player Type/LLM Player", order = 0)]
    public class LlmPlayerType : PlayerTypeBaseSo
    {
        public LlmModelPairSo modelPair;
        public LlmPromptSo promptSo;
        public string apiKeyFile = "llm_api_keys.json";

        public override void InitializeType(NavalShip navalShip)
        {
            if (navalShip is LlmAINavalShip llmAINavalShip)
            {
                //TODO Get the llm caller from the pool of callers or create one.
            }
            else
            {
                DebugUtils.DebugLogErrorMsg($"Error! Naval Ship used as LlmPlayer Type is not a LlmAINavalShip! Type is {navalShip.GetType()}!");
            }
        }
    }
}