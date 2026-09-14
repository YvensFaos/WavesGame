/*
 * Copyright (c) 2026 Yvens R Serpa [https://github.com/YvensFaos/]
 *
 * This work is licensed under the Creative Commons Attribution 4.0 International License.
 * To view a copy of this license, visit http://creativecommons.org/licenses/by/4.0/
 * or see the LICENSE file in the root directory of this repository.
 */

using System.Collections.Generic;
using UnityEngine;
using UUtils;

namespace Actors.AI.Brain
{
    public abstract class AIBrainMachine : ScriptableObject
    {
        public abstract void StartTurn(AINavalShip aiNavalShip);
        
        public abstract bool CalculateMovement(AINavalShip aiNavalShip, int stepsAvailable,
            out AIGridUnitUtility moveTo);

        public abstract bool CalculateAttack(AINavalShip aiNavalShip, out AIGridUnitUtility attack);

        public abstract AIAction CalculateAction(AINavalShip aiNavalShip, int actionsAvailable, int stepsAvailable, out AIGridUnitUtility target);

        private static void DebugUtilityChoices(AIGridUnitUtility chosenAction, int index,
            List<AIGridUnitUtility> utilities)
        {
            //TODO block this when building
            DebugUtils.DebugLogMsg($"Action {index}/{utilities.Count}: {chosenAction} chosen.",
                DebugUtils.DebugType.Regular);
            for (var i = 0; i < Mathf.Min(5, utilities.Count); i++)
            {
                DebugUtils.DebugLogMsg($"Utils => {i} {utilities[i]}", DebugUtils.DebugType.Verbose);
            }
        }
        
        protected static bool PickBestUtility(AINavalShip aiNavalShip, ref AIGridUnitUtility chosenAction,
            List<AIGridUnitUtility> utilities)
        {
            //TODO transform this into a reusable function
            if (utilities.Count == 0) return false;
            var aiGenesSo = aiNavalShip.GetGenesData();
            if (aiGenesSo.sortUtilities)
            {
                utilities.Sort();
            }

            var possibleActionsCount = Mathf.Min(utilities.Count, aiGenesSo.possibleActionsCount);
            var possibleActions = utilities.GetRange(0, possibleActionsCount);
            if (aiGenesSo.doubleBestUtilityChance)
            {
                //Add the highest utility again on the list to improve its odds
                possibleActions.Add(possibleActions[0]);
            }

            chosenAction = RandomHelper<AIGridUnitUtility>.GetRandomFromListWithIndex(possibleActions, out var index);
            DebugUtilityChoices(chosenAction, index, utilities);
            return true;
        }
    }
}