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

namespace Core.Recorder
{
    [Serializable]
    public class ActorRecordEntryJson : WavesEntryJson
    {
        [SerializeField] public string actorId;
        [SerializeField] public string faction;

        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)] [SerializeField]
        public string comment;

        public ActorRecordEntryJson(string actorId, string faction, string eventType, int turn, long timeStamp,
            string comment) : base(eventType, turn, timeStamp)
        {
            this.actorId = actorId;
            this.faction = faction;
            this.comment = string.IsNullOrEmpty(comment) ? null : comment;
        }
    }

    public abstract class ActorRecordEntry : WavesEntry
    {
        protected readonly string faction;
        protected string comment;

        protected ActorRecordEntry(string actorId, Faction faction, WavesRecordEntryType type) :
            base(type)
        {
            ActorID = actorId;
            this.faction = faction.name;
            comment = "";
        }

        public void AppendComment(string appendComment)
        {
            comment += $"{appendComment}";
        }

        protected virtual string Content()
        {
            return "";
        }

        protected override string ToJson()
        {
            return JsonConvert.SerializeObject(new ActorRecordEntryJson(ActorID, faction,
                WavesRecordEntryTypeExtensions.WavesRecordEntryTypeToString(eventType),
                turn, timeStamp, comment));
        }

        protected string ActorID { get; }
    }
}