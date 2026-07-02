using System;
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

        public InvalidResponseEntryJson(string actorId, string eventType, int turn, long timeStamp,
            string type, string message, string comment = "") : base(actorId, comment, eventType, turn, timeStamp)
        {
            this.type = type;
            this.message = message;
        }
    }

    public class InvalidResponseEntry : ActorRecordEntry
    {
        private readonly InvalidResponseType _type;
        private readonly string _message;

        public InvalidResponseEntry(string actorId, int turn, long timeStamp, InvalidResponseType type,
            string message) :
            base(actorId, WavesRecordEntryType.InvalidAttempt, turn, timeStamp)
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
            return JsonConvert.SerializeObject(new InvalidResponseEntryJson(ActorID,
                WavesRecordEntryTypeExtensions.WavesRecordEntryTypeToString(eventType), turn, timeStamp,
                _type.ToString(), _message, comment));
        }
    }
}