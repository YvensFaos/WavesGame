/*
 * Copyright (c) 2026 Yvens R Serpa [https://github.com/YvensFaos/]
 *
 * This work is licensed under the MIT License.
 * To view a copy of this license, visit https://opensource.org/licenses/MIT
 * or see the LICENSE file in the root directory of this repository.
 */

using System;
using Newtonsoft.Json;
using UUtils;

namespace Core.Recorder
{
    [Serializable]
    public class TurnRecordEntryJson : WavesEntryJson
    {
        public string realTimestamp;

        public TurnRecordEntryJson(string realTimestamp, string eventType, int turn, long timeStamp) : base(eventType,
            turn, timeStamp)
        {
            this.realTimestamp = realTimestamp;
        }
    }

    public class TurnRecordEntry : WavesEntry
    {
        private readonly string _realTimestamp;

        public TurnRecordEntry() : base(WavesRecordEntryType.Turn)
        {
            _realTimestamp = TimestampHelper.GetSimplifiedUTCTimestamp();
        }

        public override void PerformEntry()
        {
            //TODO
            //if (!LevelController.TryToGetSingleton(out var _)) return;
        }

        protected override string ToJson()
        {
            return JsonConvert.SerializeObject(new TurnRecordEntryJson(_realTimestamp,
                WavesRecordEntryTypeExtensions.WavesRecordEntryTypeToString(WavesRecordEntryType.Turn), turn,
                timeStamp), GetJsonSerializerSettings());
        }
    }
}