/*
 * Copyright (c) 2026 Yvens R Serpa [https://github.com/YvensFaos/]
 *
 * This work is licensed under the Creative Commons Attribution 4.0 International License.
 * To view a copy of this license, visit http://creativecommons.org/licenses/by/4.0/
 * or see the LICENSE file in the root directory of this repository.
 */

using System;
using Actors;
using UnityEngine;
using UUtils;

namespace Core.Recorder
{
    [Serializable]
    public class EndGameRecordEntryJson : WavesEntryJson
    {
        [SerializeField] public string goalMessage;
        [SerializeField] public string winningFaction;

        public EndGameRecordEntryJson(string eventType, int turn, long timeStamp, string goalMessage,
            string winningFaction) : base(eventType, turn, timeStamp)
        {
            this.goalMessage = goalMessage;
            this.winningFaction = winningFaction;
        }
    }

    public class EndGameRecordEntry : WavesEntry
    {
        private readonly string _goalMessage;
        private readonly Faction _winningFaction;

        public EndGameRecordEntry(string goalMessage, Faction winningFaction, int turn, long timeStamp) : base(
            WavesRecordEntryType.EndGame, turn, timeStamp)
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

        protected override string ToJson()
        {
            return JsonUtility.ToJson(new EndGameRecordEntryJson(
                WavesRecordEntryTypeExtensions.WavesRecordEntryTypeToString(WavesRecordEntryType.EndGame), turn,
                timeStamp, _goalMessage, _winningFaction.name));
        }

        /// TODO change this to read the entry from a JSON.
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

            return new EndGameRecordEntry(levelGoalMessage, winningFaction, -1, -1);
        }
    }
}