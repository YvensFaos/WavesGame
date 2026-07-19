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
    [Serializable]
    public class CommandRecordEntryJson : ActorRecordEntryJson
    {
        [SerializeField] public SimpleVector2Int movement;
        [SerializeField] public SimpleVector2Int attack;
        [SerializeField] public SimpleVector2Int moveAfterAttack;

        public CommandRecordEntryJson(string actorId, string faction, string eventType, int turn, long timeStamp, Vector2Int movement,
            Vector2Int attack, Vector2Int moveAfterAttack, string comment = "")
            : base(actorId, faction, eventType, turn, timeStamp, comment)
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

        public CommandRecordEntry(string actorId, Faction faction, Vector2Int movement, Vector2Int attack,
            Vector2Int moveAfterAttack)
            : base(actorId, faction, WavesRecordEntryType.Command)
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
            return JsonConvert.SerializeObject(new CommandRecordEntryJson(ActorID, faction,
                WavesRecordEntryTypeExtensions.WavesRecordEntryTypeToString(eventType), turn, timeStamp,
                _movement, _attack, _moveAfterAttack, comment));
        }
    }
}