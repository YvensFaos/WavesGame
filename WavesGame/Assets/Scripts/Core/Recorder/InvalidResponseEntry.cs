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
    public enum InvalidResponseType
    {
        Exception,
        NoResponse,
        InvalidOutput
    }

    [Serializable]
    public class InvalidResponseEntryJson : ActorRecordEntryJson
    {
        [SerializeField] public string type;
        [SerializeField] public string message;

        public InvalidResponseEntryJson(string actorId, string faction, string eventType, int turn, long timeStamp,
            string type, string message, string comment = "") : base(actorId, faction, eventType, turn, timeStamp,
            comment)
        {
            this.type = type;
            this.message = message;
        }
    }

    public class InvalidResponseEntry : ActorRecordEntry
    {
        private readonly InvalidResponseType _type;
        private readonly string _message;

        public InvalidResponseEntry(string actorId, Faction faction, InvalidResponseType type,
            string message) :
            base(actorId, faction, WavesRecordEntryType.InvalidAttempt)
        {
            _type = type;
            _message = message;
        }

        public override void PerformEntry()
        {
            DebugUtils.DebugLogMsg(
                $"Invalid response. Message: {_message}.",
                DebugUtils.DebugType.Verbose);
        }

        protected override string ToJson()
        {
            return JsonConvert.SerializeObject(new InvalidResponseEntryJson(ActorID, faction,
                WavesRecordEntryTypeExtensions.WavesRecordEntryTypeToString(eventType), turn, timeStamp,
                _type.ToString(), _message, comment));
        }
    }
}