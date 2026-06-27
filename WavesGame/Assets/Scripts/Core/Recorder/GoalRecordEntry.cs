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
    public class GoalRecordEntryJson : WavesEntryJson
    {
        [SerializeField] public string goalMessage;

        public GoalRecordEntryJson(string eventType, int turn, long timeStamp, string goalMessage) : base(eventType,
            turn, timeStamp)
        {
            this.goalMessage = goalMessage;
        }
    }

    public class GoalRecordEntry : WavesEntry
    {
        private readonly string _goalMessage;

        public GoalRecordEntry(LevelGoal levelGoal) : base(WavesRecordEntryType.Goal, -1, 0)
        {
            _goalMessage = LevelGoalTypeExtension.LevelGoalTypeToString(levelGoal.Type());
        }

        private GoalRecordEntry(string levelGoalMessage) : base(WavesRecordEntryType.Goal, -1, 0)
        {
            _goalMessage = levelGoalMessage;
        }

        public override void PerformEntry()
        {
            if (!LevelController.TryToGetSingleton(out var levelController)) return;
            //TODO does not handle specific type variables such as targets and number of turns to survive
            levelController.GetLevelGoal().SetTypeViaString(_goalMessage);
            DebugUtils.DebugLogMsg($"Level goal changed to: {_goalMessage}.", DebugUtils.DebugType.Temporary);
        }

        protected override string ToJson()
        {
            return JsonUtility.ToJson(new GoalRecordEntryJson(
                WavesRecordEntryTypeExtensions.WavesRecordEntryTypeToString(WavesRecordEntryType.Goal), turn, timeStamp,
                _goalMessage));
        }

        /// TODO change this to read the entry from a JSON.
        public static GoalRecordEntry MakeRecordEntryFromString(string entryString)
        {
            // GOAL;Destroy All Targets
            var parts = entryString.Split(";");
            if (parts.Length < 2)
            {
                return null;
            }

            var levelGoalMessage = parts[1];

            return new GoalRecordEntry(levelGoalMessage);
        }
    }
}