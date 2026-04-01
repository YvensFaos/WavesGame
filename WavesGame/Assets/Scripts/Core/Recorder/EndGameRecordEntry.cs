/*
 * Copyright (c) 2026 Yvens R Serpa [https://github.com/YvensFaos/]
 *
 * This work is licensed under the Creative Commons Attribution 4.0 International License.
 * To view a copy of this license, visit http://creativecommons.org/licenses/by/4.0/
 * or see the LICENSE file in the root directory of this repository.
 */

using Actors;
using UnityEngine;
using UUtils;
using UUtils.GameRecorder;

namespace Core.Recorder
{
    public class EndGameRecordEntry : RecordEntry
    {
        public static readonly string EndGameRecordType = "OVER";

        private readonly string _goalMessage;
        private Faction _winningFaction;

        public EndGameRecordEntry(string goalMessage, Faction winningFaction)
        {
            _goalMessage = goalMessage;
            _winningFaction = winningFaction;
        }

        public override void PerformEntry()
        {
            var winnerMessage = _winningFaction != null ? $"Winning Faction is {_winningFaction}!" : "";
            DebugUtils.DebugLogMsg($"End game reached! {_goalMessage}.{winnerMessage}", DebugUtils.DebugType.Temporary);
            if (!LevelController.TryToGetSingleton(out var levelController)) return;
            levelController.ForceFinishLevel();
        }

        public sealed override string ToString()
        {
            var winningFactionEntry = _winningFaction != null ? $";{_winningFaction}" : "";
            return $"{EndGameRecordType};{_goalMessage}{winningFactionEntry}";
        }

        public static EndGameRecordEntry MakeRecordEntryFromString(string entryString)
        {
            // OVER;Destroy All Targets;White
            var parts = entryString.Split(";");
            if (parts.Length < 2)
            {
                return null;
            }

            var levelGoalMessage = parts[1];
            Faction winningFaction = null;
            if (parts.Length > 2)
            {
                var factionName = parts[2];
                //TODO convert from the name to a faction
                winningFaction = ScriptableObject.CreateInstance<Faction>();
                winningFaction.name = factionName;
            }

            return new EndGameRecordEntry(levelGoalMessage, winningFaction);
        }
    }
}