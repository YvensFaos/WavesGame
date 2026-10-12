using System;
using Actors;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.InputSystem.Utilities;
using UUtils;

namespace Core.Recorder
{
    [Serializable]
    public class ActorInputRecordEntryJson : InputRecordEntryJson
    {
        [SerializeField] public string actorId;
        [SerializeField] public string faction;
        [SerializeField] public bool isHumanTurn;

        public ActorInputRecordEntryJson(string actorId, string faction, bool isHumanTurn, string actionName,
            string actionMap,
            string actionPhase, string actionValue, string actionDevice, double actionStartTime, string eventType,
            int turn, long timeStamp) : base(actionName, actionMap, actionPhase, actionValue, actionDevice,
            actionStartTime, eventType, turn, timeStamp)
        {
            this.actorId = actorId;
            this.faction = faction;
            this.isHumanTurn = isHumanTurn;
        }
    }

    public class ActorInputRecordEntry : InputRecordEntry
    {
        private readonly string _actorId;
        private readonly string _faction;
        private readonly bool _isHumanTurn;

        public ActorInputRecordEntry(string actorId, Faction faction, bool isHumanTurn,
            InputActionTrace.ActionEventPtr actionEventPtr) :
            base(actionEventPtr)
        {
            _actorId = actorId;
            _faction = faction.name;
            _isHumanTurn = isHumanTurn;
        }

        public override void PerformEntry()
        {
            var human = _isHumanTurn ? "Y" : "N";
            DebugUtils.DebugLogMsg(
                $"Input from {_actorId}[{_faction}] [Human?{human}]: {ActionName}, {ActionPhase}, {ActionValue}, {ActionDevice}, {ActionStartTime}.",
                DebugUtils.DebugType.Verbose);
        }

        protected override string ToJson()
        {
            return JsonConvert.SerializeObject(new ActorInputRecordEntryJson(_actorId, _faction, _isHumanTurn,
                ActionName, ActionMap,
                ActionPhase,
                ActionValue, ActionDevice, ActionStartTime,
                WavesRecordEntryTypeExtensions.WavesRecordEntryTypeToString(eventType),
                turn, timeStamp), GetJsonSerializerSettings());
        }
    }
}