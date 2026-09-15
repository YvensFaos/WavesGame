/*
 * Copyright (c) 2026 Yvens R Serpa [https://github.com/YvensFaos/]
 *
 * This work is licensed under the Creative Commons Attribution 4.0 International License.
 * To view a copy of this license, visit http://creativecommons.org/licenses/by/4.0/
 * or see the LICENSE file in the root directory of this repository.
 */

using System;
using System.Collections.Generic;
using Actors;
using Actors.AI;
using Actors.AI.LlmAI;
using Grid;
using Newtonsoft.Json;
using UnityEngine;
using UUtils;

namespace Core.Recorder
{
    [Serializable]
    public class ScenarioEntryJson : WavesEntryJson
    {
        [SerializeField] public List<string> scenarioOverview;

        public ScenarioEntryJson(List<string> scenarioOverview, string eventType, int turn, long timeStamp) : base(
            eventType, turn, timeStamp)
        {
            this.scenarioOverview = scenarioOverview;
        }
    }

    public class ScenarioEntry : WavesEntry
    {
        private List<string> _scenarioOverview;

        public ScenarioEntry(GridManager gridManager) : base(WavesRecordEntryType.ScenarioState)
        {
            GenerateScenarioOverview(gridManager);
        }

        private void GenerateScenarioOverview(GridManager gridManager)
        {
            var grid = gridManager.Grid();
            var dimensions = gridManager.GetDimensions();
            _scenarioOverview = new List<string>();

            for (var index = grid.Count - 1; index > -1;)
            {
                var row = "";
                for (var j = 0; j < dimensions.x && index < grid.Count; j++)
                {
                    var gridUnit = grid[index--];
                    if (gridUnit.IsEmpty())
                    {
                        row = gridUnit.Type() switch
                        {
                            GridUnitType.Moveable => ".",
                            GridUnitType.Blocked => "#",
                            _ => "!"
                        } + row;
                    }
                    else
                    {
                        var actor = gridUnit.GetActor();
                        switch (actor)
                        {
                            case AINavalShip aiNavalShip:
                                AddFactionLetterToRowList(ref row, aiNavalShip);
                                break;
                            case LlmAINavalShip llmAINavalShip:
                                AddFactionLetterToRowList(ref row, llmAINavalShip);
                                break;
                            case AIBaseShip aiBaseShip:
                                AddFactionLetterToRowList(ref row, aiBaseShip);
                                break;
                            case NavalShip navalShip:
                                AddFactionLetterToRowList(ref row, navalShip);
                                break;
                            case NavalTarget:
                                row = "@" + row;
                                break;
                            case NavalActor:
                                row = "%" + row;
                                break;
                            case WaveActor waveActor:
                                var waveDirection = waveActor.GetWaveDirection;
                                row = GridMoveTypeExtensions.GridMovementSimplifiedSymbol(waveDirection) + row;
                                break;
                            case ObstacleActor obstacleActor:
                                row = "&" + row;
                                break;
                            default:
                                row = "?" + row;
                                break;
                        }
                    }
                }

                _scenarioOverview.Add(row);
            }

            return;

            void AddFactionLetterToRowList(ref string row, NavalShip navalShip)
            {
                //Gets the first letter of the faction
                row = $"{navalShip.GetFaction().ToString()[0]}{row}";
            }
        }

        public override void PerformEntry()
        {
            DebugUtils.DebugLogMsg($"TODO", DebugUtils.DebugType.Temporary);
        }

        protected override string ToJson()
        {
            return JsonConvert.SerializeObject(new ScenarioEntryJson(_scenarioOverview,
                WavesRecordEntryTypeExtensions.WavesRecordEntryTypeToString(WavesRecordEntryType.ScenarioState), turn,
                timeStamp), GetJsonSerializerSettings());
        }
    }
}