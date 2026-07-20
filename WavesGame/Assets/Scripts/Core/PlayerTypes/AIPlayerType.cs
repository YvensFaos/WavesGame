/*
 * Copyright (c) 2026 Yvens R Serpa [https://github.com/YvensFaos/]
 *
 * This work is licensed under the Creative Commons Attribution 4.0 International License.
 * To view a copy of this license, visit http://creativecommons.org/licenses/by/4.0/
 * or see the LICENSE file in the root directory of this repository.
 */

using System.Collections.Generic;
using Actors;
using Actors.AI;
using Actors.AI.Brain;
using UnityEngine;
using UUtils;

namespace Core.PlayerTypes
{
    [CreateAssetMenu(fileName = "AI Player Type", menuName = "Waves/Player Type/AI Player", order = 2)]
    public class AIPlayerType : PlayerTypeBaseSo
    {
        public AIGenesSO aiGenesSo;
        public AIBrainMachine aiBrainMachine;
        
        public override void InitializeType(NavalShip navalShip, HashSet<Faction> factions)
        {
            if (navalShip is AINavalShip aiNavalShip)
            {
                aiNavalShip.GenesData = aiGenesSo;
                aiNavalShip.ChangeBrainTo(aiBrainMachine);
                aiNavalShip.UpdateName();
            }
            else
            {
                DebugUtils.DebugLogErrorMsg(
                    $"Error! Naval Ship used as AiPlayerType Type is not a AiPlayerType! Type is {navalShip.GetType()}!");
            }
        }

        public override string GetName()
        {
            return $"AIPlayerType-{aiBrainMachine}-{aiGenesSo.name}";
        }
    }
}