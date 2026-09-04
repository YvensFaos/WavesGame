using System;
using Actors;
using Actors.AI;
using Actors.AI.LlmAI;
using Newtonsoft.Json;
using UnityEngine;

namespace Core.Recorder
{
    [Serializable]
    public class NavalActorEntryJson
    {
        [SerializeField] public string name;
        [SerializeField] public int startingHealth;
        [SerializeField] public string shipPrefabType;
        [SerializeField] public string faction;
        [SerializeField] public string shipData;
        [SerializeField] public string navalCannon;
        [SerializeField] public SimpleVector2Int position;

        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)] [SerializeField]
        public string genesData = null;
        
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
            startingHealth = navalActor.GetCurrentHealth();
            var shipType = navalActor.GetType();
            shipPrefabType = shipType.Name;
            position = new SimpleVector2Int(navalActor.GetUnit().Index());

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
                    genesData = aiNavalShip.GetGenesData().name;
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