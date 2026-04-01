/*
 * Copyright (c) 2026 Yvens R Serpa [https://github.com/YvensFaos/]
 *
 * This work is licensed under the Creative Commons Attribution 4.0 International License.
 * To view a copy of this license, visit http://creativecommons.org/licenses/by/4.0/
 * or see the LICENSE file in the root directory of this repository.
 */

using UUtils;
using UUtils.GameRecorder;

namespace Core.Recorder
{
    public class GoalRecordEntry : RecordEntry
    {
        public static readonly string GoalRecordType = "GOAL";

        private readonly string _goalMessage;

        public GoalRecordEntry(LevelGoal levelGoal)
        {
            _goalMessage = LevelGoalTypeExtension.LevelGoalTypeToString(levelGoal.Type());
        }

        public GoalRecordEntry(string levelGoalMessage)
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

        public sealed override string ToString()
        {
            return $"{GoalRecordType};{_goalMessage}";
        }

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