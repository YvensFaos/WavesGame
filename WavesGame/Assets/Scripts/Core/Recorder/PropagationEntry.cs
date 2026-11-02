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
using UUtils;

namespace Core.Recorder
{
    [Serializable]
    public class PropagationEntryJson : WavesEntryJson
    {
        [SerializeField] [JsonProperty(DefaultValueHandling = DefaultValueHandling.Include)]
        private string originId;

        [SerializeField] [JsonProperty(DefaultValueHandling = DefaultValueHandling.Include)]
        private string targetId;

        [SerializeField] [JsonProperty(DefaultValueHandling = DefaultValueHandling.Include)]
        private int damage;

        public PropagationEntryJson(string eventType, int turn, long timeStamp, string originId, string targetId,
            int damage) :
            base(eventType, turn, timeStamp)
        {
            this.originId = originId;
            this.targetId = targetId;
            this.damage = damage;
        }
    }

    public class PropagationEntry : WavesEntry
    {
        private string _originId;
        private string _targetId;
        private int _damage;

        public PropagationEntry(string originId, string targetId, int damage) : base(WavesRecordEntryType.Propagation)
        {
            _originId = originId;
            _targetId = targetId;
            _damage = damage;
        }

        public override void PerformEntry()
        {
            DebugUtils.DebugLogMsg($"Waves Propagation. Origin: {_originId}. Target: {_targetId}.",
                DebugUtils.DebugType.Temporary);
        }

        protected override string ToJson()
        {
            return JsonConvert.SerializeObject(new PropagationEntryJson(
                WavesRecordEntryTypeExtensions.WavesRecordEntryTypeToString(WavesRecordEntryType.Propagation), turn,
                timeStamp, _originId, _targetId, _damage), GetJsonSerializerSettings());
        }
    }
}