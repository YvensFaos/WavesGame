/*
 * Copyright (c) 2026 Yvens R Serpa [https://github.com/YvensFaos/]
 *
 * This work is licensed under the Creative Commons Attribution 4.0 International License.
 * To view a copy of this license, visit http://creativecommons.org/licenses/by/4.0/
 * or see the LICENSE file in the root directory of this repository.
 */

using System;
using UUtils;

namespace Core.Recorder
{
    [Serializable]
    public class DamageRecordEntry : ActorRecordEntry
    {
        private int _damage;

        public DamageRecordEntry(string actorId, int damage) : base(actorId)
        {
            _damage = damage;
            type = WavesRecordEntryType.Damage;
        }

        protected override string Content()
        {
            return $";{_damage}";
        }

        public override void PerformEntry()
        {
            DebugUtils.DebugLogMsg($"DamageRecordEntry: {ActorID} was damaged by {_damage}.", DebugUtils.DebugType.Temporary);
            var levelController = LevelController.GetSingleton();
            var navalActor = levelController.GetNavalActorWithId(ActorID);
            if (navalActor == null) return;
            navalActor.TakeDirectDamage(_damage);
        }

        public float Damage => _damage;
        
        /// <summary>
        /// Returns a DamageRecordEntry built from a string in the format "DAMG;[actorId];[damage]".
        /// If the format does not comply, then the method returns null.
        /// </summary>
        /// <param name="entryString"></param>
        /// <returns></returns>
        public static DamageRecordEntry MakeRecordEntryFromString(string entryString)
        {
            // DAMG;Target;3
            var parts = entryString.Split(";");
            if (parts.Length < 3)
            {
                return null;
            }

            var actorId = parts[1];
            if (!int.TryParse(parts[2], out var damage))
            {
                return null;
            }

            return new DamageRecordEntry(actorId, damage);
        }
    }
}