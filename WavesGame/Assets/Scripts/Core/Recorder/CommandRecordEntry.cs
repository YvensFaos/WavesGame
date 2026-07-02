using System;
using Newtonsoft.Json;
using UnityEngine;
using UUtils;

namespace Core.Recorder
{
    [Serializable]
    public class CommandRecordEntryJson : ActorRecordEntryJson
    {
        [SerializeField] public SimpleVector2Int movement;
        [SerializeField] public SimpleVector2Int attack;
        [SerializeField] public SimpleVector2Int moveAfterAttack;

        public CommandRecordEntryJson(string actorId, string eventType, int turn, long timeStamp, Vector2Int movement,
            Vector2Int attack, Vector2Int moveAfterAttack, string comment = "")
            : base(actorId, comment, eventType, turn, timeStamp)
        {
            this.movement = new SimpleVector2Int(movement);
            this.attack = new SimpleVector2Int(attack);
            this.moveAfterAttack = new SimpleVector2Int(moveAfterAttack);
        }
    }

    public class CommandRecordEntry : ActorRecordEntry
    {
        private readonly Vector2Int _movement;
        private readonly Vector2Int _attack;
        private readonly Vector2Int _moveAfterAttack;

        public CommandRecordEntry(string actorId, int turn, long timeStamp, Vector2Int movement, Vector2Int attack,
            Vector2Int moveAfterAttack)
            : base(actorId, WavesRecordEntryType.Command, turn, timeStamp)
        {
            _movement = movement;
            _attack = attack;
            _moveAfterAttack = moveAfterAttack;
        }

        public override void PerformEntry()
        {
            DebugUtils.DebugLogMsg(
                $"Command - Movement: {_movement};  Attack: {_attack};  Move After Attack: {_moveAfterAttack}.",
                DebugUtils.DebugType.Verbose);
        }

        protected override string ToJson()
        {
            return JsonConvert.SerializeObject(new CommandRecordEntryJson(ActorID,
                WavesRecordEntryTypeExtensions.WavesRecordEntryTypeToString(eventType), turn, timeStamp,
                _movement, _attack, _moveAfterAttack, comment));
        }
    }
}