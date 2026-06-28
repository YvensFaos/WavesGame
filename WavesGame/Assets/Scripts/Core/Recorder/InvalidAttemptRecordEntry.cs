using System;
using Grid;
using Newtonsoft.Json;
using UnityEngine;
using UUtils;

namespace Core.Recorder
{
    public enum InvalidAttemptType
    {
        Move,
        Attack,
        InvalidTarget
    }

    [Serializable]
    public class InvalidAttemptRecordEntryJson : ActorRecordEntryJson
    {
        [SerializeField] public string type;
        [SerializeField] public SimpleVector2Int position;
        [SerializeField] public string targetId;
        [SerializeField] public SimpleVector2Int targetPosition;
        [SerializeField] public string reasoning;

        public InvalidAttemptRecordEntryJson(string actorId,
            string eventType, int turn, long timeStamp,
            string type, Vector2Int position,
            GridActor targetActor, Vector2Int targetUnit,
            string reasoning, string comment = "") :
            base(actorId, comment, eventType, turn, timeStamp)
        {
            this.type = type;
            this.position = new SimpleVector2Int(position);
            targetId = targetActor != null ? targetActor.name : "";
            targetPosition = new SimpleVector2Int(targetUnit);
            this.reasoning = reasoning;
        }
    }

    public class InvalidAttemptRecordEntry : ActorRecordEntry
    {
        private readonly InvalidAttemptType _type;
        private readonly Vector2Int _position;
        private readonly GridActor _targetActor;
        private readonly Vector2Int _targetPosition;
        private readonly string _reasoning;

        public InvalidAttemptRecordEntry(string actorId, int turn, long timeStamp,
            InvalidAttemptType type, Vector2Int position, GridActor targetActor, Vector2Int targetPosition,
            string reasoning) :
            base(actorId, WavesRecordEntryType.InvalidAttempt, turn, timeStamp)
        {
            _type = type;
            _position = position;
            _targetActor = targetActor;
            _targetPosition = targetPosition;
            _reasoning = reasoning;
        }

        public override void PerformEntry()
        {
            DebugUtils.DebugLogMsg(
                $"Invalid attempt. Reasoning: {_reasoning}.",
                DebugUtils.DebugType.Verbose);
        }

        protected override string ToJson()
        {
            return JsonConvert.SerializeObject(new InvalidAttemptRecordEntryJson(ActorID,
                WavesRecordEntryTypeExtensions.WavesRecordEntryTypeToString(eventType), turn, timeStamp,
                _type.ToString(), _position, _targetActor, _targetPosition, _reasoning, comment));
        }
    }
}