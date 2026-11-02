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
    public class EndGameRecordEntryJson : WavesEntryJson
    {
        [SerializeField] public string goalMessage;
        [SerializeField] public string winningFaction;
        [SerializeField] public string finalTime;

        public EndGameRecordEntryJson(string eventType, int turn, string finalTime, long timeStamp, string goalMessage,
            string winningFaction) : base(eventType, turn, timeStamp)
        {
            this.goalMessage = goalMessage;
            this.winningFaction = winningFaction;
            this.finalTime = finalTime;
        }
    }

    public class EndGameRecordEntry : WavesEntry
    {
        private readonly string _goalMessage;
        private readonly bool _victory;
        private readonly Faction _winningFaction;
        private readonly string _finalTime;

        public EndGameRecordEntry(string goalMessage, Faction winningFaction, bool victory) : base(
            WavesRecordEntryType.EndGame)
        {
            _goalMessage = goalMessage;
            _winningFaction = winningFaction;
            _victory = victory;
            _finalTime = TimestampHelper.GetSimplifiedUTCTimestamp();
        }

        public override void PerformEntry()
        {
            var winnerMessage = _winningFaction != null ? $"Winning Faction is {_winningFaction}!" :
                _victory ? $"Player Victory!" : "Player Lost!";
            DebugUtils.DebugLogMsg($"End game reached! {_goalMessage}.{winnerMessage}", DebugUtils.DebugType.Temporary);
            //TODO
            // if (!LevelController.TryToGetSingleton(out var levelController)) return;
            // levelController.ForceFinishLevel();
        }

        protected override string ToJson()
        {
            var winningFactionName = _winningFaction != null ? $"Winning Faction is {_winningFaction}!" :
                _victory ? $"Player Victory!" : "Player Lost!";
            return JsonConvert.SerializeObject(new EndGameRecordEntryJson(
                WavesRecordEntryTypeExtensions.WavesRecordEntryTypeToString(WavesRecordEntryType.EndGame), turn,
                _finalTime,
                timeStamp, _goalMessage, winningFactionName), GetJsonSerializerSettings());
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
            if (parts.Length <= 2)
                return null; //new EndGameRecordEntry(levelGoalMessage, winningFaction, false, -1, -1);
            var factionName = parts[2];
            //TODO convert from the name to a faction
            var winningFaction = ScriptableObject.CreateInstance<Faction>();
            winningFaction.name = factionName;

            return null; //new EndGameRecordEntry(levelGoalMessage, winningFaction, false, -1, -1);
        }
    }
}