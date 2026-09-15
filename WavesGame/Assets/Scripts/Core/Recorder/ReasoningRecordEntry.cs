/*
 * Copyright (c) 2026 Yvens R Serpa [https://github.com/YvensFaos/]
 *
 * This work is licensed under the Creative Commons Attribution 4.0 International License.
 * To view a copy of this license, visit http://creativecommons.org/licenses/by/4.0/
 * or see the LICENSE file in the root directory of this repository.
 */

using System;
using Actors;
using Newtonsoft.Json;
using UnityEngine;
using UUtils;

namespace Core.Recorder
{
    [Serializable]
    public class ReasoningRecordEntryJson : ActorRecordEntryJson
    {
        [SerializeField] public string reasoning;
        [SerializeField] [JsonProperty(DefaultValueHandling = DefaultValueHandling.Include)]
        public float responseTime;

        public ReasoningRecordEntryJson(string actorId, string faction, string eventType, int turn, long timeStamp,
            string reasoning, float responseTime,
            string comment = "") : base(actorId, faction, eventType, turn, timeStamp, comment)
        {
            this.reasoning = reasoning;
            this.responseTime = responseTime;
        }
    }

    public class ReasoningRecordEntry : ActorRecordEntry
    {
        private readonly string _reasoning;
        private float _responseTime;

        public ReasoningRecordEntry(string actorId, Faction faction,
            string reasoning, float responseTime) : base(actorId, faction, WavesRecordEntryType.Reasoning)
        {
            _reasoning = reasoning;
            _responseTime = responseTime;
        }

        public override void PerformEntry()
        {
            DebugUtils.DebugLogMsg(
                $"Reasoning: {_reasoning}.",
                DebugUtils.DebugType.Verbose);
        }

        protected override string ToJson()
        {
            return JsonConvert.SerializeObject(new ReasoningRecordEntryJson(ActorID, faction,
                WavesRecordEntryTypeExtensions.WavesRecordEntryTypeToString(eventType), turn, timeStamp, _reasoning, _responseTime,
                comment), GetJsonSerializerSettings());
        }
    }
}