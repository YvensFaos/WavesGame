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
using UnityEngine;
using UUtils;

namespace Core.Recorder
{
    [Serializable]
    public class AttackRecordEntry : ActorRecordEntry
    {
        private Vector2Int _attackPosition;
        private float _damage;

        public AttackRecordEntry(string actorId, Vector2Int attackPosition, float damage) : base(actorId)
        {
            _attackPosition = attackPosition;
            _damage = damage;
            type = WavesRecordEntryType.Attack;
        }

        protected override string Content()
        {
            return $";{_attackPosition};{_damage}";
        }

        public override void PerformEntry()
        {
            DebugUtils.DebugLogMsg($"AttackRecordEntry: {ActorID} attacks at {_attackPosition}. Recorded damage: {_damage}.", DebugUtils.DebugType.Temporary);
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

        public Vector2Int AttackPosition => _attackPosition;
        public float Damage => _damage;
        
        /// <summary>
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

            return new AttackRecordEntry(actorId, new Vector2Int(x, y), damage);
        }
    }
}