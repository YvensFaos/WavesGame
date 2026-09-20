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
using Core.Recorder.Extras;
using Newtonsoft.Json;
using UnityEngine;

namespace Core.Recorder
{
    [Serializable]
    public class GeneEntryJson
    {
        [SerializeField] [JsonConverter(typeof(FloatRoundingConverter))] public float aggressiveness;
        [SerializeField] [JsonConverter(typeof(FloatRoundingConverter))] public float finisherWeight;
        [SerializeField] [JsonConverter(typeof(FloatRoundingConverter))] public float patience;
        [SerializeField] [JsonConverter(typeof(FloatRoundingConverter))] public float friendliness;
        [SerializeField] [JsonConverter(typeof(FloatRoundingConverter))] public float selfPreservation;
        [SerializeField] [JsonConverter(typeof(FloatRoundingConverter))] public float awareness;
        [SerializeField] [JsonConverter(typeof(FloatRoundingConverter))] public float sight;
        [SerializeField] [JsonConverter(typeof(FloatRoundingConverter))] public float targetInterest;
        [SerializeField] public bool sortUtilities;
        [SerializeField] public int topUtilitiesChosen;
        [SerializeField] [JsonConverter(typeof(FloatRoundingConverter))] public float decay;

        public GeneEntryJson(AIGenesSO genes)
        {
            aggressiveness = MathF.Round(genes.aggressiveness, 2);
            finisherWeight = MathF.Round(genes.finisherWeight, 2);
            patience = MathF.Round(genes.patience, 2);
            friendliness = MathF.Round(genes.friendliness, 2);
            selfPreservation = MathF.Round(genes.selfPreservation, 2);
            awareness = MathF.Round(genes.awareness, 2);
            sight = MathF.Round(genes.sight, 2);
            targetInterest = MathF.Round(genes.targetInterest, 2);
            sortUtilities = genes.sortUtilities;
            topUtilitiesChosen = genes.topUtilitiesChosen;
            decay = MathF.Round(genes.decay, 2);
        }
    }

    [Serializable]
    public class NavalActorShortEntryJson
    {
        [SerializeField] public string name;
        [SerializeField, JsonProperty(DefaultValueHandling = DefaultValueHandling.Include)] public int currentHealth;
        [SerializeField] public string faction;
        [SerializeField] public SimpleVector2Int position;

        public NavalActorShortEntryJson(string name, int currentHealth, string faction, SimpleVector2Int position)
        {
            this.name = name;
            this.currentHealth = currentHealth;
            this.faction = faction;
            this.position = position;
        }

        public NavalActorShortEntryJson(NavalActor navalActor)
        {
            name = navalActor.name;
            currentHealth = navalActor.GetCurrentHealth();
            var gridUnit = navalActor.GetUnit();
            position = gridUnit != null ? new SimpleVector2Int(gridUnit.Index()) : new SimpleVector2Int(-1, -1);
            faction = Faction.GetNeutralFaction().name;
            if (navalActor is NavalShip navalShip)
            {
                faction = navalShip.GetFaction().ToString();
            }
        }
    }

    [Serializable]
    public class NavalActorEntryJson
    {
        [SerializeField] public string name;
        [SerializeField, JsonProperty(DefaultValueHandling = DefaultValueHandling.Include)] public int currentHealth;
        [SerializeField] public string shipPrefabType;
        [SerializeField] public string faction;
        [SerializeField] public string shipData;
        [SerializeField] public string navalCannon;
        [SerializeField] public SimpleVector2Int position;

        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore, NullValueHandling = NullValueHandling.Ignore)]
        [SerializeField]
        public GeneEntryJson genesData = null;

        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore, NullValueHandling = NullValueHandling.Ignore)]
        [SerializeField]
        public string machineBrain = null;

        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore, NullValueHandling = NullValueHandling.Ignore)]
        [SerializeField]
        public string basePrompt = null;

        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore, NullValueHandling = NullValueHandling.Ignore)]
        [SerializeField]
        public string llmInfo = null;

        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore, NullValueHandling = NullValueHandling.Ignore)]
        [SerializeField]
        public string llmType = null;

        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore, NullValueHandling = NullValueHandling.Ignore)]
        [SerializeField]
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
                    llmModel = llmCaller.GetModel();
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