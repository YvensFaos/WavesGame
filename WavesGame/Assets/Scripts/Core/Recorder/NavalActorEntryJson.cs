/*
 * Copyright (c) 2026 Yvens R Serpa [https://github.com/YvensFaos/]
 *
 * This work is licensed under the Creative Commons Attribution 4.0 International License.
 * To view a copy of this license, visit http://creativecommons.org/licenses/by/4.0/
 * or see the LICENSE file in the root directory of this repository.
 */

using System;
using Actors;
using Actors.AI;
using Actors.AI.LlmAI;
using Newtonsoft.Json;
using UnityEngine;

namespace Core.Recorder
{
    [Serializable]
    public class GeneEntryJson
    {
        [SerializeField] public float aggressiveness;
        [SerializeField] public float patience;
        [SerializeField] public float friendliness;
        [SerializeField] public float selfPreservation;
        [SerializeField] public float awareness;
        [SerializeField] public float sight;
        [SerializeField] public float targetInterest;
        [SerializeField] public bool sortUtilities;
        [SerializeField] public int topUtilitiesChosen;
        [SerializeField] public float decay;

        public GeneEntryJson(AIGenesSO genes)
        {
            aggressiveness = genes.aggressiveness;
            patience = genes.patience;
            friendliness = genes.friendliness;
            selfPreservation = genes.selfPreservation;
            awareness = genes.awareness;
            sight = genes.sight;
            targetInterest = genes.targetInterest;
            sortUtilities = genes.sortUtilities;
            topUtilitiesChosen = genes.topUtilitiesChosen;
            decay = genes.decay;
        }
    }
    
    [Serializable]
    public class NavalActorEntryJson
    {
        [SerializeField] public string name;
        [SerializeField] public int currentHealth;
        [SerializeField] public string shipPrefabType;
        [SerializeField] public string faction;
        [SerializeField] public string shipData;
        [SerializeField] public string navalCannon;
        [SerializeField] public SimpleVector2Int position;

        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)] [SerializeField]
        public GeneEntryJson genesData = null;
        
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)] [SerializeField]
        public string machineBrain = null;

        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)] [SerializeField]
        public string basePrompt = null;

        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)] [SerializeField]
        public string llmInfo = null;

        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)] [SerializeField]
        public string llmType = null;

        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)] [SerializeField]
        public string llmModel = null;

        public NavalActorEntryJson(NavalActor navalActor)
        {
            name = navalActor.name;
            currentHealth = navalActor.GetCurrentHealth();
            var shipType = navalActor.GetType();
            shipPrefabType = shipType.Name;
            var gridUnit = navalActor.GetUnit();
            position = gridUnit != null ? new SimpleVector2Int(gridUnit.Index()) : new SimpleVector2Int(-1, -1);

            faction = "Missing Type";
            shipData = "No Ship Data";
            navalCannon = "No Naval Cannon";
            basePrompt = null;
            llmInfo = null;
            llmType = null;
            llmModel = null;
            genesData = null;
            machineBrain = null;

            switch (navalActor)
            {
                case NavalTarget: faction = "None"; break;
                case LlmAINavalShip llmAINavalShip:
                {
                    basePrompt = llmAINavalShip.GetPrompt().name;
                    llmInfo = llmAINavalShip.GetLlmInfo();
                    var llmCaller = llmAINavalShip.GetCaller();
                    llmType = llmCaller.GetLlmType().ToString();
                    llmModel = llmCaller.GetLlmModel();

                    GetInfoFromNavalShip(llmAINavalShip);
                }
                    break;

                case AINavalShip aiNavalShip:
                {
                    genesData = new GeneEntryJson(aiNavalShip.GetGenesData());
                    machineBrain = aiNavalShip.GetBrain().name;
                    
                    GetInfoFromNavalShip(aiNavalShip);
                }
                    break;

                case NavalShip navalShip:
                {
                    GetInfoFromNavalShip(navalShip);
                }
                    break;
            }

            return;

            void GetInfoFromNavalShip(NavalShip navalShip)
            {
                faction = navalShip.GetFaction().ToString();
                shipData = navalShip.ShipData.name;
                navalCannon = navalShip.NavalCannon.GetCannonDataName();
            }
        }
    }
}