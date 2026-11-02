/*
 * Copyright (c) 2026 Yvens R Serpa [https://github.com/YvensFaos/]
 *
 * This work is licensed under the MIT License.
 * To view a copy of this license, visit https://opensource.org/licenses/MIT
 * or see the LICENSE file in the root directory of this repository.
 */

using System;
using Core;
using FALLA;
using NaughtyAttributes;
using UnityEngine;
using UUtils;

namespace Actors.AI.LlmAI
{
    [Serializable]
    public class FactionLlmPair : Pair<Faction, LlmModelPairSo>
    {
        [SerializeField, ReadOnly] private LlmSingleCallerObject llmSingleCaller;
        [SerializeField] private bool matchModel;
        [SerializeField] private LlmPromptSo promptSo;

        [Header("For Custom LLM Type and Regular AIs")] [SerializeField]
        private AIBaseShip aiBaseShipPrefab;

        [field: SerializeField] public AIGenesSO AIGenes { get; set; }

        public FactionLlmPair(Faction one, LlmModelPairSo two) : base(one, two)
        {
        }

        public void SetCaller(LlmSingleCallerObject llmSingleCallerObject)
        {
            llmSingleCaller = llmSingleCallerObject;
        }

        public LlmSingleCallerObject LlmSingleCaller => llmSingleCaller;

        public AIBaseShip AIBaseShipPrefab => aiBaseShipPrefab;

        public LlmPromptSo GetPromptSo() => promptSo;

        public override string ToString()
        {
            var promptName = promptSo == null ? "" : $"-{promptSo.name}";
            var aiGenes = AIGenes == null ? "" : $"-{AIGenes.name}";
            return $"{Two.modelPair}{promptName}{aiGenes}";
        }
    }
}