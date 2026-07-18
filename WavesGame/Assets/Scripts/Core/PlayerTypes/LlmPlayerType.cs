/*
 * Copyright (c) 2026 Yvens R Serpa [https://github.com/YvensFaos/]
 *
 * This work is licensed under the Creative Commons Attribution 4.0 International License.
 * To view a copy of this license, visit http://creativecommons.org/licenses/by/4.0/
 * or see the LICENSE file in the root directory of this repository.
 */

using System.Collections.Generic;
using System.Linq;
using Actors;
using Actors.AI.LlmAI;
using FALLA;
using UnityEngine;
using UUtils;

namespace Core.PlayerTypes
{
    [CreateAssetMenu(fileName = "LLM Player Type", menuName = "Waves/Player Type/LLM Player", order = 0)]
    public class LlmPlayerType : PlayerTypeBaseSo
    {
        public LlmModelPairSo modelPair;
        public string typeKey;
        public LlmPromptSo promptSo;
        public LlmCallerObject callerObjectPrefab;

        public override void InitializeType(NavalShip navalShip, HashSet<Faction> factions)
        {
            if (navalShip is LlmAINavalShip llmAINavalShip)
            {
                var caller = Instantiate(callerObjectPrefab, navalShip.transform);
                var llmModelPair = modelPair.modelPair; 
                caller.Initialize(llmModelPair.One, llmModelPair.Two, typeKey);
                llmAINavalShip.SetCaller(caller);
                llmAINavalShip.ChangeBasePrompt(promptSo);
                llmAINavalShip.UpdateName();

                foreach (var faction in factions.Where(faction => !faction.Equals(navalShip.GetFaction())))
                {
                    llmAINavalShip.AddEnemyFaction(faction);
                }
            }
            else
            {
                DebugUtils.DebugLogErrorMsg(
                    $"Error! Naval Ship used as LlmPlayer Type is not a LlmAINavalShip! Type is {navalShip.GetType()}!");
            }
        }

        public string GetName()
        {
            return $"LlmPlayerType-{modelPair}-{promptSo.name}";
        }
    }
}