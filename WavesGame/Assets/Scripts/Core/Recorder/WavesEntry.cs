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
using UUtils.GameRecorder;

namespace Core.Recorder
{
    [Serializable]
    public class WavesEntryJson
    {
        [JsonProperty(Order = -2)] [SerializeField]
        public string eventType;

        [JsonProperty(Order = -1)] [SerializeField]
        public int turn;

        [JsonProperty(Order = 0)] [SerializeField]
        public long timeStamp;

        public WavesEntryJson(string eventType, int turn, long timeStamp)
        {
            this.eventType = eventType;
            this.turn = turn;
            this.timeStamp = timeStamp;
        }
    }

    public abstract class WavesEntry : RecordEntry
    {
        protected readonly WavesRecordEntryType eventType;
        protected readonly int turn;
        protected readonly long timeStamp;

        protected WavesEntry(WavesRecordEntryType eventType, int turn, long timeStamp)
        {
            this.eventType = eventType;
            this.turn = turn;
            this.timeStamp = timeStamp;
        }

        public sealed override string ToString()
        {
            return ToJson();
        }

        protected virtual string ToJson()
        {
            return JsonUtility.ToJson(
                new WavesEntryJson(WavesRecordEntryTypeExtensions.WavesRecordEntryTypeToString(eventType), turn,
                    timeStamp));
        }
    }
}