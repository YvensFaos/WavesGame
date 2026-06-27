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
    [Serializable]
    public class AttackRecordEntryJson : ActorRecordEntryJson
    {
        [SerializeField] public string targetId;
        [SerializeField] public SimpleVector2Int attackPosition;
        [SerializeField] public float damage;

        public AttackRecordEntryJson(string actorId, string eventType, int turn, long timeStamp,
            string targetId, Vector2Int attackPosition, float damage, string comment = "") : base(actorId, comment,
            eventType, turn,
            timeStamp)
        {
            this.targetId = targetId;
            this.attackPosition = new SimpleVector2Int(attackPosition);
            this.damage = damage;
        }
    }

    [Serializable]
    public class AttackRecordEntry : ActorRecordEntry
    {
        private string _targetId;
        private Vector2Int _attackPosition;
        private float _damage;

        public AttackRecordEntry(string actorId, Vector2Int attackPosition, string targetId, float damage, int turn,
            long timeStamp) :
            base(actorId, WavesRecordEntryType.Attack, turn, timeStamp)
        {
            _attackPosition = attackPosition;
            _targetId = targetId;
            _damage = damage;
        }

        protected override string Content()
        {
            return $";{_attackPosition};{_damage};{_targetId}";
        }

        public override void PerformEntry()
        {
            DebugUtils.DebugLogMsg(
                $"AttackRecordEntry: {ActorID} attacks at {_attackPosition} targeting {_targetId}. Recorded damage: {_damage}.",
                DebugUtils.DebugType.Verbose);
            // Animate attack at given position. Animate only. Damage is handled by the DAMG entry.
            if (!GridManager.GetSingleton().CheckGridPosition(_attackPosition, out var attackGridPosition)) return;
            var actorsCount = attackGridPosition.ActorsCount();
            if (actorsCount == 1)
            {
                var actor = attackGridPosition.GetActor();
                AnimateAttackOnActor(actor);
            }
            else if (attackGridPosition.ActorsCount() > 1)
            {
                var enumerator = attackGridPosition.GetActorEnumerator();
                while (enumerator.MoveNext())
                {
                    AnimateAttackOnActor(enumerator.Current);
                }

                enumerator.Dispose();
            }

            return;

            void AnimateAttackOnActor(GridActor actor)
            {
                if (actor is NavalActor navalActor)
                {
                    navalActor.AnimateDamage(_damage > 0);
                }
            }
        }

        protected override string ToJson()
        {
            //string actorId, string eventType, int turn, long timeStamp, string targetId, Vector2Int attackPosition, float damage, string comment = ""
            return JsonConvert.SerializeObject(new AttackRecordEntryJson(ActorID,
                WavesRecordEntryTypeExtensions.WavesRecordEntryTypeToString(eventType), turn, timeStamp, _targetId,
                _attackPosition, _damage, comment));
        }

        /// <summary>
        /// TODO change this to read the entryString as JSON
        /// Returns a AttackRecordEntry built from a string in the format "ATTK;[actorId];([x], [y]);[damage]".
        /// If the format does not comply, then the method returns null.
        /// </summary>
        /// <param name="entryString"></param>
        /// <returns></returns>
        public static AttackRecordEntry MakeRecordEntryFromString(string entryString)
        {
            // ATTK;SimpleBoat;(10, 11);7
            var parts = entryString.Split(";");
            if (parts.Length < 4)
            {
                return null;
            }

            var actorId = parts[1];
            var positionString = parts[2];
            var positions = positionString.Substring(1, positionString.Length - 2).Split(",");
            if (!int.TryParse(positions[0], out var x))
            {
                return null;
            }

            if (!int.TryParse(positions[1], out var y))
            {
                return null;
            }

            if (!int.TryParse(parts[3], out var damage))
            {
                return null;
            }

            //TODO update reading the turn and timeStamp
            return new AttackRecordEntry(actorId, new Vector2Int(x, y), "TODO <target>", damage, -1, -1);
        }
    }
}