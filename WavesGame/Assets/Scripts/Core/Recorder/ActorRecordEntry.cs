/*
 * Copyright (c) 2026 Yvens R Serpa [https://github.com/YvensFaos/]
 *
 * This work is licensed under the Creative Commons Attribution 4.0 International License.
 * To view a copy of this license, visit http://creativecommons.org/licenses/by/4.0/
 * or see the LICENSE file in the root directory of this repository.
 */

using System;
using Newtonsoft.Json;
using UnityEngine;

namespace Core.Recorder
{
    [Serializable]
    public class ActorRecordEntryJson : WavesEntryJson
    {
        [SerializeField] public string actorId;
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        [SerializeField] public string comment;

        public ActorRecordEntryJson(string actorId, string comment, string eventType, int turn, long timeStamp) : base(
            eventType, turn,
            timeStamp)
        {
            this.actorId = actorId;
            this.comment = string.IsNullOrEmpty(comment) ? null : comment;
        }
    }

    public abstract class ActorRecordEntry : WavesEntry
    {
        protected string comment;

        protected ActorRecordEntry(string actorId, WavesRecordEntryType type, int turn, long timeStamp) :
            base(type, turn, timeStamp)
        {
            ActorID = actorId;
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
            return JsonConvert.SerializeObject(new ActorRecordEntryJson(ActorID, comment,
                WavesRecordEntryTypeExtensions.WavesRecordEntryTypeToString(eventType), turn, timeStamp));
        }

        protected string ActorID { get; }
    }
}