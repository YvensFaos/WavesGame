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
    public class MovementRecordEntryJson : ActorRecordEntryJson
    {
        [SerializeField] public SimpleVector2Int moveFrom;
        [SerializeField] public SimpleVector2Int moveTo;

        public MovementRecordEntryJson(string actorId, string eventType, int turn, long timeStamp,
            Vector2Int moveFrom, Vector2Int moveTo, string comment = "") : base(actorId, comment, eventType, turn,
            timeStamp)
        {
            this.moveFrom = new SimpleVector2Int(moveFrom);
            this.moveTo = new SimpleVector2Int(moveTo);
        }
    }

    [Serializable]
    public class MovementRecordEntry : ActorRecordEntry
    {
        public MovementRecordEntry(string actorId, Vector2Int moveFrom, Vector2Int moveTo, int turn, long timeStamp) :
            base(actorId, WavesRecordEntryType.Movement, turn, timeStamp)
        {
            MoveTo = moveTo;
            MoveFrom = moveFrom;
        }

        protected override string Content()
        {
            return $";MoveFrom:{MoveFrom};MoveTo:{MoveTo}";
        }

        public override void PerformEntry()
        {
            DebugUtils.DebugLogMsg($"MovementRecordEntry: {ActorID} moves from {MoveFrom} to {MoveTo}.",
                DebugUtils.DebugType.Temporary);
            var levelController = LevelController.GetSingleton();
            var navalShip = levelController.GetNavalShipWithId(ActorID);
            if (navalShip == null) return;
            levelController.MoveActor(navalShip, MoveTo);
        }

        protected override string ToJson()
        {
            return JsonConvert.SerializeObject(new MovementRecordEntryJson(ActorID,
                WavesRecordEntryTypeExtensions.WavesRecordEntryTypeToString(WavesRecordEntryType.Movement), turn,
                timeStamp, MoveFrom, MoveTo, comment));
        }

        public Vector2Int MoveFrom { get; }
        public Vector2Int MoveTo { get; }

        /// <summary>
        /// TODO change this to read the entry from a JSON.
        /// Returns a MovementRecordEntry built from a string in the format "MOVE;[actorId];([x], [y])".
        /// If the format does not comply, then the method returns null.
        /// </summary>
        /// <param name="entryString"></param>
        /// <returns></returns>
        public static MovementRecordEntry MakeRecordEntryFromString(string entryString)
        {
            // MOVE;SimpleBoat;(9, 9)
            var parts = entryString.Split(";");
            if (parts.Length < 3)
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

            return new MovementRecordEntry(actorId, new Vector2Int(-1, -1), new Vector2Int(x, y), -1, -1);
        }
    }
}