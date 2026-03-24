/*
 * Copyright (c) 2026 Yvens R Serpa [https://github.com/YvensFaos/]
 *
 * This work is licensed under the Creative Commons Attribution 4.0 International License.
 * To view a copy of this license, visit http://creativecommons.org/licenses/by/4.0/
 * or see the LICENSE file in the root directory of this repository.
 */

using System;
using UnityEngine;
using UUtils;

namespace Core.Recorder
{
    [Serializable]
    public class MovementRecordEntry : ActorRecordEntry
    {
        private Vector2Int _moveTo;

        public MovementRecordEntry(string actorId, Vector2Int moveTo) : base(actorId)
        {
            _moveTo = moveTo;
            type = WavesRecordEntryType.Movement;
        }

        protected override string Content()
        {
            return $";{_moveTo}";
        }

        public override void PerformEntry()
        {
            DebugUtils.DebugLogMsg($"MovementRecordEntry: {ActorID} moves to {_moveTo}.", DebugUtils.DebugType.Temporary);
            var levelController = LevelController.GetSingleton();
            var navalShip = levelController.GetNavalShipWithId(ActorID);
            if (navalShip == null) return;
            levelController.MoveActor(navalShip, _moveTo);
        }

        public Vector2Int MoveTo => _moveTo;

        /// <summary>
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

            return new MovementRecordEntry(actorId, new Vector2Int(x, y));
        }
    }
}