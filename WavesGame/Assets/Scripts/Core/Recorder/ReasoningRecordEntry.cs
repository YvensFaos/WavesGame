using System;
using Newtonsoft.Json;
using UnityEngine;
using UUtils;

namespace Core.Recorder
{
    [Serializable]
    public class ReasoningRecordEntryJson : ActorRecordEntryJson
    {
        [SerializeField] public string reasoning;

        public ReasoningRecordEntryJson(string actorId, string eventType, int turn, long timeStamp, string reasoning,
            string comment = "") : base(actorId, comment, eventType, turn, timeStamp)
        {
            this.reasoning = reasoning;
        }
    }

    public class ReasoningRecordEntry : ActorRecordEntry
    {
        private string _reasoning;

        public ReasoningRecordEntry(string actorId, int turn, long timeStamp,
            string reasoning) : base(actorId, WavesRecordEntryType.Reasoning, turn, timeStamp)
        {
            _reasoning = reasoning;
        }

        public override void PerformEntry()
        {
            DebugUtils.DebugLogMsg(
                $"Reasoning: {_reasoning}.",
                DebugUtils.DebugType.Verbose);
        }

        protected override string ToJson()
        {
            //string actorId, string eventType, int turn, long timeStamp, string targetId, Vector2Int attackPosition, float damage, string comment = ""
            return JsonConvert.SerializeObject(new ReasoningRecordEntryJson(ActorID,
                WavesRecordEntryTypeExtensions.WavesRecordEntryTypeToString(eventType), turn, timeStamp, _reasoning, comment));
        }
    }
}