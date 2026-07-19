/*
 * Copyright (c) 2026 Yvens R Serpa [https://github.com/YvensFaos/]
 *
 * This work is licensed under the Creative Commons Attribution 4.0 International License.
 * To view a copy of this license, visit http://creativecommons.org/licenses/by/4.0/
 * or see the LICENSE file in the root directory of this repository.
 */

using System;
using Actors;
using Grid;
using Newtonsoft.Json;
using UnityEngine;
using UUtils;

namespace Core.Recorder
{
    public enum InvalidAttemptType
    {
        FailedToMove,
        OutOfReach,
        InvalidTarget,
        FriendlyFire
    }

    [Serializable]
    public class InvalidAttemptRecordEntryJson : ActorRecordEntryJson
    {
        [SerializeField] public string type;
        [SerializeField] public SimpleVector2Int position;
        [SerializeField] public string targetId;
        [SerializeField] public SimpleVector2Int targetPosition;
        [SerializeField] public string reasoning;

        public InvalidAttemptRecordEntryJson(string actorId, string faction,
            string eventType, int turn, long timeStamp,
            string type, Vector2Int position,
            GridActor targetActor, Vector2Int targetUnit,
            string reasoning, string comment = "") :
            base(actorId, faction, eventType, turn, timeStamp, comment)
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

        public InvalidAttemptRecordEntry(string actorId, Faction faction, int turn, long timeStamp,
            InvalidAttemptType type, Vector2Int position, GridActor targetActor, Vector2Int targetPosition,
            string reasoning) :
            base(actorId, faction, WavesRecordEntryType.InvalidAttempt)
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
            return JsonConvert.SerializeObject(new InvalidAttemptRecordEntryJson(ActorID, faction,
                WavesRecordEntryTypeExtensions.WavesRecordEntryTypeToString(eventType), turn, timeStamp,
                _type.ToString(), _position, _targetActor, _targetPosition, _reasoning, comment));
        }
    }
}