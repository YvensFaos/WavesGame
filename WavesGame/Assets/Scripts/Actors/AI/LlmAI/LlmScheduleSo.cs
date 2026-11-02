/*
 * Copyright (c) 2026 Yvens R Serpa [https://github.com/YvensFaos/]
 *
 * This work is licensed under the MIT License.
 * To view a copy of this license, visit https://opensource.org/licenses/MIT
 * or see the LICENSE file in the root directory of this repository.
 */

using System.Collections.Generic;
using System.Linq;
using Core;
using FALLA;
using NaughtyAttributes;
using UnityEngine;
using UUtils;

namespace Actors.AI.LlmAI
{
    [CreateAssetMenu(fileName = "LlmSchedule", menuName = "Waves/LLM/Schedule", order = 1)]
    public class LlmScheduleSo : ScriptableObject
    {
        public int repetitions;
        [SerializeField, ReadOnly] private int internalRepetitionsCount;
        public List<FactionLlmPair> factionPairs;

        public int InternalRepetitionsCount => internalRepetitionsCount;

        [Button("Initialize")]
        public void Initialize()
        {
            internalRepetitionsCount = repetitions;
        }

        public bool Use()
        {
            internalRepetitionsCount = Mathf.Max(0, InternalRepetitionsCount - 1);
            DebugUtils.DebugLogMsg(
                $"Update Schedule repetition, _internalRepetitionsCount -> {InternalRepetitionsCount}.",
                DebugUtils.DebugType.System);
            return InternalRepetitionsCount <= 0;
        }

        public List<Faction> GetFactions()
        {
            return factionPairs.Select(pair => pair.One).ToList();
        }

        public FactionLlmPair GetFactionPair(Faction faction)
        {
            return factionPairs.Find(pair => pair.One.Equals(faction));
        }

        public override string ToString()
        {
            var str = "";
            factionPairs.ForEach(pair =>
            {
                if (pair.Two.modelPair.One != LlmType.Custom)
                {
                    str += $"{pair}" + "=";
                }
                else
                {
                    var customPrefab = pair.AIBaseShipPrefab;
                    if (pair.AIBaseShipPrefab is AINavalShip navalShip)
                    {
                        //navalShip.GenesData
                        var genes = pair.AIGenes == null ? navalShip.GenesData.name : pair.AIGenes.name;
                        str += $"{customPrefab.name}{genes}" + "=";
                    }
                    else
                    {
                        str += $"{customPrefab}" + "=";
                    }
                }
            });
            if (str.Length > 0)
            {
                str = str[..^1];
            }

            return str;
        }
    }
}