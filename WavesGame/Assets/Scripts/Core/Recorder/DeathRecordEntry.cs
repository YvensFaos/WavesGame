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
    public class DeathRecordEntry : ActorRecordEntry
    {
        public DeathRecordEntry(string actorId) : base(actorId)
        {
            type = WavesRecordEntryType.Death;
        }

        public override void PerformEntry()
        {
            DebugUtils.DebugLogMsg($"DeathRecordEntry: {ActorID} is destroyed.", DebugUtils.DebugType.Temporary);
            var levelController = LevelController.GetSingleton();
            var navalActor = levelController.GetNavalActorWithId(ActorID);
            navalActor.DestroyActorImmediate();
        }
        
        /// <summary>
        /// Returns a DeathRecordEntry built from a string in the format "DEAD;[actorId]".
        /// If the format does not comply, then the method returns null.
        /// </summary>
        /// <param name="entryString"></param>
        /// <returns></returns>
        public static DeathRecordEntry MakeRecordEntryFromString(string entryString)
        {
            // DEAD;Target
            var parts = entryString.Split(";");
            if (parts.Length < 2)
            {
                return null;
            }

            var actorId = parts[1];

            return new DeathRecordEntry(actorId);
        }
    }
}