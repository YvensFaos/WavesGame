using System;
using System.Collections.Generic;
using System.Linq;
using Actors;
using Actors.AI;
using Actors.AI.LlmAI;
using Newtonsoft.Json;
using UnityEngine;
using UUtils;

namespace Core.Recorder
{
    [Serializable]
    public class NavalActorEntryJson
    {
        [SerializeField] public string name;
        [SerializeField] public string shipPrefabType;
        [SerializeField] public string faction;
        [SerializeField] public string shipData;
        [SerializeField] public string navalCannon;
        [SerializeField] public SimpleVector2Int initialPosition;

        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)] [SerializeField]
        public string genesData = null;

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
            var shipType = navalActor.GetType();
            shipPrefabType = shipType.Name;
            initialPosition = new SimpleVector2Int(navalActor.GetUnit().Index());

            faction = "Missing Type";
            shipData = "No Ship Data";
            navalCannon = "No Naval Cannon";
            basePrompt = null;
            llmInfo = null;
            llmType = null;
            llmModel = null;
            genesData = null;

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
                    genesData = aiNavalShip.GetGenesData().name;
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

    [Serializable]
    public class WavesGameInfoRecordEntryJson : WavesEntryJson
    {
        [SerializeField] public string map;
        [SerializeField] public int randomSeed;
        [SerializeField] public List<NavalActorEntryJson> navalActorEntryJsons;

        public WavesGameInfoRecordEntryJson(string eventType, int turn, long timeStamp, string map, int randomSeed,
            List<NavalActor> navalActors) : base(eventType, turn, timeStamp)
        {
            this.map = map;
            this.randomSeed = randomSeed;
            navalActorEntryJsons = navalActors.Select(ship => new NavalActorEntryJson(ship)).ToList();
        }
    }

    public class WavesGameInfoEntry : WavesEntry
    {
        private readonly string _map;
        private readonly int _randomSeed;
        private readonly List<NavalActor> _navalActors;

        public WavesGameInfoEntry(string map, int randomSeed, List<NavalActor> navalActors) : base(
            WavesRecordEntryType.Information, 0, -1)
        {
            _map = map;
            _randomSeed = randomSeed;
            _navalActors = navalActors;
        }

        public override void PerformEntry()
        {
            DebugUtils.DebugLogMsg($"Waves Game Info: {_map}. Random Seed: {_randomSeed}.",
                DebugUtils.DebugType.Temporary);
        }

        protected override string ToJson()
        {
            return JsonUtility.ToJson(new WavesGameInfoRecordEntryJson(
                WavesRecordEntryTypeExtensions.WavesRecordEntryTypeToString(WavesRecordEntryType.Information), turn,
                timeStamp, _map, _randomSeed, _navalActors));
        }
    }
}