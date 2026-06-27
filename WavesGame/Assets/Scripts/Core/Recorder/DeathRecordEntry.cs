/*
 * Copyright (c) 2026 Yvens R Serpa [https://github.com/YvensFaos/]
 *
 * This work is licensed under the Creative Commons Attribution 4.0 International License.
 * To view a copy of this license, visit http://creativecommons.org/licenses/by/4.0/
 * or see the LICENSE file in the root directory of this repository.
 */

using System;
using Newtonsoft.Json;
using UnityEngine;
using UUtils;

namespace Core.Recorder
{
    [Serializable]
    public class DeathRecordEntryJson : ActorRecordEntryJson
    {
        public DeathRecordEntryJson(string actorId, string eventType, int turn, long timeStamp, string comment = "") :
            base(actorId, comment, eventType, turn, timeStamp)
        {
        }
    }

    [Serializable]
    public class DeathRecordEntry : ActorRecordEntry
    {
        public DeathRecordEntry(string actorId, int turn, long timeStamp) : base(actorId, WavesRecordEntryType.Death,
            turn, timeStamp)
        {
        }

        public override void PerformEntry()
        {
            DebugUtils.DebugLogMsg($"DeathRecordEntry: {ActorID} is destroyed.", DebugUtils.DebugType.Temporary);
            var levelController = LevelController.GetSingleton();
            var navalActor = levelController.GetNavalActorWithId(ActorID);
            navalActor.DestroyActorImmediate();
        }

        protected override string ToJson()
        {
            return JsonConvert.SerializeObject(new DeathRecordEntryJson(ActorID,
                WavesRecordEntryTypeExtensions.WavesRecordEntryTypeToString(WavesRecordEntryType.Death), turn,
                timeStamp, comment));
        }

        /// <summary>
        /// TODO change this to read the entry from a JSON.
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

            return new DeathRecordEntry(actorId, -1, -1);
        }
    }
}