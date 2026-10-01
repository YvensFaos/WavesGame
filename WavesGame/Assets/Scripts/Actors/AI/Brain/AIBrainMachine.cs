/*
 * Copyright (c) 2026 Yvens R Serpa [https://github.com/YvensFaos/]
 *
 * This work is licensed under the Creative Commons Attribution 4.0 International License.
 * To view a copy of this license, visit http://creativecommons.org/licenses/by/4.0/
 * or see the LICENSE file in the root directory of this repository.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using NaughtyAttributes;
using UnityEngine;
using UUtils;
using Random = UnityEngine.Random;

namespace Actors.AI.Brain
{
    public abstract class AIBrainMachine : ScriptableObject
    {
        [SerializeField]
        protected AIUtilitySelection selection;
        [SerializeField]
        protected bool showHeatmap;

        [SerializeField, EnableIf("showHeatmap")]
        protected HeatMapTile heatMapTilePrefab;
        
        public abstract void StartTurn(AINavalShip aiNavalShip);
        
        public abstract bool CalculateMovement(AINavalShip aiNavalShip, int stepsAvailable, bool afterAttack,
            out AIGridUnitUtility moveTo);

        public abstract bool CalculateAttack(AINavalShip aiNavalShip, out AIGridUnitUtility attack);

        public abstract AIAction CalculateAction(AINavalShip aiNavalShip, int actionsAvailable, int stepsAvailable, bool afterAttack, out AIGridUnitUtility target);

        private static void DebugUtilityChoices(AIGridUnitUtility chosenAction, int index,
            List<AIGridUnitUtility> utilities)
        {
            #if UNITY_EDITOR
            DebugUtils.DebugLogMsg($"Action {index}/{utilities.Count}: {chosenAction} chosen.",
                DebugUtils.DebugType.Regular);
            for (var i = 0; i < Mathf.Min(5, utilities.Count); i++)
            {
                DebugUtils.DebugLogMsg($"Utils => {i} {utilities[i]}", DebugUtils.DebugType.Verbose);
            }
            #endif
        }

        protected void DisplayHeatMap(List<AIGridUnitUtility> utilities, float destroyAfter = 2.0f)
        {
            if (!showHeatmap) return;
            DebugUtils.DebugLogMsg($"Displaying heat map for {destroyAfter} seconds.", DebugUtils.DebugType.Temporary);
            var utilitiesExtreme = CalculateUtilitiesExtreme();
            var normalize = (utilitiesExtreme.y - utilitiesExtreme.x);
            normalize = normalize == 0.0f ? 1.0f : normalize;
            foreach(var utility in utilities)
            {
                var heatTile = Instantiate(heatMapTilePrefab, utility.GetUnit().transform);
                var ratio = utilitiesExtreme.x;
                if (!float.IsInfinity(utility.Utility))
                {
                    
                    ratio = (utility.Utility - utilitiesExtreme.x) / normalize;
                }
                
                DebugUtils.DebugLogMsg($"{utility.GetUnit()} heat is {ratio}. U:{utility.Utility} -> [{utilitiesExtreme.x}, {utilitiesExtreme.y}]", DebugUtils.DebugType.Temporary);
                heatTile.ShowHeatTile(ratio, destroyAfter);
            }

            return;

            Vector2 CalculateUtilitiesExtreme()
            {
                var extreme = new Vector2(float.MaxValue, float.MinValue);
                foreach (var utilityValue in utilities.Select(utility => utility.Utility).Where(utilityValue => !float.IsInfinity(utilityValue)))
                {
                    if (utilityValue < extreme.x)
                    {
                        extreme.x = utilityValue;
                    }

                    if (utilityValue > extreme.y)
                    {
                        extreme.y = utilityValue;
                    }
                }

                return extreme;
            }
        }
        
        protected bool PickBestUtility(AINavalShip aiNavalShip, ref AIGridUnitUtility chosenAction,
            List<AIGridUnitUtility> utilities)
        {
            if (utilities.Count == 0) return false;
            var aiGenesSo = aiNavalShip.GetGenesData();
            if (aiGenesSo.sortUtilities)
            {
                utilities.Sort();
            }
            var possibleActionsCount = Mathf.Min(utilities.Count, aiGenesSo.topUtilitiesChosen);
            var possibleActions = utilities.GetRange(0, possibleActionsCount);
            int index;
            switch (selection)
            {
                case AIUtilitySelection.UniformDistribution:
                    chosenAction = UniformDistribution(possibleActions, out index);
                    break;
                case AIUtilitySelection.BoostedBestUtility:
                    chosenAction = BoostedBestUtility(possibleActions, out index);
                    break;
                case AIUtilitySelection.RankDecay:
                    var decay = aiGenesSo.decay;
                    chosenAction = RankDecay(possibleActions, decay, out index);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            
            DebugUtilityChoices(chosenAction, index, utilities);
            return true;
        }

        private static AIGridUnitUtility UniformDistribution(List<AIGridUnitUtility> utilities, out int index)
        {
            return RandomHelper<AIGridUnitUtility>.GetRandomFromListWithIndex(utilities, out index);
        }
        
        private static AIGridUnitUtility BoostedBestUtility(List<AIGridUnitUtility> utilities, out int index)
        {
            //Boost the highest utility
            utilities.Add(utilities[0]);
            return RandomHelper<AIGridUnitUtility>.GetRandomFromListWithIndex(utilities, out index);
        }

        private static AIGridUnitUtility RankDecay(List<AIGridUnitUtility> utilities, float decay, out int index)
        {
            var totalWeight = 0f;
            var utilitiesCount = utilities.Count;
            var weights = new float[utilitiesCount];
            var r = 1.0f;
            for (var i = 0; i < utilitiesCount; i++)
            {
                weights[i] = r;
                totalWeight += r;
                r *= decay;
            }
            var chance = Random.value * totalWeight;
            var acc = 0f;
            index = -1;
            for (var i = 0; i < utilitiesCount; i++)
            {
                acc += weights[i];
                if (!(chance < acc)) continue;
                index = i;
                return utilities[i];
            }
            return utilities[0];
        }
        
        public override string ToString()
        {
            return $"[{selection}]";
        }
    }
}