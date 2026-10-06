/*
 * Copyright (c) 2026 Yvens R Serpa [https://github.com/YvensFaos/]
 *
 * This work is licensed under the Creative Commons Attribution 4.0 International License.
 * To view a copy of this license, visit http://creativecommons.org/licenses/by/4.0/
 * or see the LICENSE file in the root directory of this repository.
 */

using System.Collections.Generic;
using System.Linq;
using Grid;
using UnityEngine;

namespace Actors.AI.LlmAI
{
    public static class LlmAIPromptGenerator
    {
        public static string GeneratePrompt(LlmAINavalShip llmAINavalShip, LlmPromptSo templatePrompt,
            List<Faction> enemyFactions)
        {
            var template = templatePrompt.prompt;
            var selfFaction = llmAINavalShip.GetFaction();
            var faction = selfFaction.ToString();
            template = ReplaceTagWithText(template, "faction", faction);
            template = ReplaceTagWithText(template, "enemy_factions", ListEnemyFactions(enemyFactions));
            var shipData = llmAINavalShip.ShipData;
            template = ReplaceTagWithText(template, "move_count", shipData.stats.speed.Two.ToString());
            var cannonData = llmAINavalShip.NavalCannon;
            template = ReplaceTagWithText(template, "attack_range", cannonData.GetCannonSo.area.ToString());
            template = ReplaceTagWithText(template, "attack_offset", cannonData.GetCannonSo.deadZone.ToString());
            template = ReplaceTagWithText(template, "cannon", cannonData.ToString());
            template = ReplaceTagWithText(template, "self_status", llmAINavalShip.ToLlmString());
            template = ReplaceTagWithText(template, "health", llmAINavalShip.GetCurrentHealth().ToString());
            var index = llmAINavalShip.GetUnit().Index();
            template = ReplaceTagWithText(template, "self_x", index.x.ToString());
            template = ReplaceTagWithText(template, "self_y", index.y.ToString());
            template = ReplaceTagWithText(template, "movement_range", shipData.stats.speed.Two.ToString());
            var gridManager = GridManager.GetSingleton();
            var dimensions = gridManager.GetDimensions();
            template = ReplaceTagWithText(template, "grid_size", dimensions.ToString());

            var walkableUnits = gridManager
                .GetGridUnitsInRadiusManhattan(index, llmAINavalShip.RemainingSteps);
            var movementPositions = ListGridUnitIndicesToString(walkableUnits);
            template = ReplaceTagWithText(template, "movement_positions", movementPositions);

            var attackableActorsInRadiusManhattan =
                gridManager.GetAttackableActorsInRadiusManhattan(index, cannonData.GetCannonSo,
                    llmAINavalShip.RemainingSteps);
            var attackableActors =
                ListGridUnitListActorsPairToString(attackableActorsInRadiusManhattan, selfFaction, "\r\n");
            template = ReplaceTagWithText(template, "attack_enabling_tiles", attackableActors);

            var safePositions = walkableUnits.FindAll(position => position.IsEmpty());
            template = ReplaceTagWithText(template, "safe_movement_positions",
                ListGridUnitIndicesToString(safePositions, true, "\r\n"));

            var nearbyWavePositions = walkableUnits.FindAll(position => position.HasActorOfType<WaveActor>());
            template = ReplaceTagWithText(template, "wave_movement_positions",
                ListGridUnitIndicesToString(nearbyWavePositions, false, "\r\n"));

            var nearbyShipsPositions = new List<GridActor>();
            walkableUnits.ForEach(position =>
            {
                if (position.Index().Equals(index) || !position.GetFirstActorOfType<GridActor>(out var actor)) return;
                if (actor is not WaveActor)
                {
                    nearbyShipsPositions.Add(actor);
                }
            });
            template = ReplaceTagWithText(template, "blocked_movement_positions",
                ListGridActorsIndicesToString(nearbyShipsPositions, selfFaction, true, "\r\n"));

            var attackableUnits = gridManager
                .GetAttackableUnitsInRadiusManhattan(index, cannonData.GetCannonSo, llmAINavalShip.RemainingSteps);
            attackableUnits = attackableUnits.FindAll(unit => !unit.IsEmpty());

            template = ReplaceTagWithText(template, "possible_attack_positions",
                ListGridUnitsToString(attackableUnits, templatePrompt));

            var currentAttackableUnits = gridManager
                .GetGridUnitsForMoveType(cannonData.GetCannonSo.targetAreaType, index, cannonData.GetCannonSo.area,
                    cannonData.GetCannonSo.deadZone);
            var currentAttackableActors = new List<GridActor>();
            currentAttackableUnits.ForEach(position =>
            {
                if (position.Index().Equals(index) || !position.GetFirstActorOfType<GridActor>(out var actor)) return;
                if (actor is NavalShip navalShip)
                {
                    if (navalShip.GetFaction() != selfFaction)
                    {
                        currentAttackableActors.Add(actor);
                    }
                }
                else
                {
                    currentAttackableActors.Add(actor);
                }
            });

            template = ReplaceTagWithText(template, "current_attack_positions",
                ListGridActorsIndicesToString(currentAttackableActors, selfFaction, true, "\r\n"));

            var grid = gridManager.Grid();

            template = ReplaceTagWithText(template, "grid_overview",
                ListGridToString(llmAINavalShip, grid, templatePrompt.includeEmptySpaces));

            var enemiesOnTheGrid = grid.FindAll(gridUnit =>
            {
                var actor = gridUnit.GetActor();
                var isEnemy = actor is NavalShip navalShip && navalShip.GetFaction() != selfFaction;
                var isAIEnemy = actor is AINavalShip aiNavalShip && aiNavalShip.GetFaction() != selfFaction;
                var isLlmAiEnemy = actor is LlmAINavalShip llmAI && llmAI.GetFaction() != selfFaction;
                return isEnemy || isAIEnemy || isLlmAiEnemy;
            });
            template = ReplaceTagWithText(template, "grid_overview_enemies",
                ListGridToString(llmAINavalShip, enemiesOnTheGrid, templatePrompt.includeEmptySpaces));

            var wavesOnTheGrid = grid.FindAll(gridUnit =>
            {
                var actor = gridUnit.GetActor();
                return actor is WaveActor;
            });
            template = ReplaceTagWithText(template, "grid_overview_waves",
                ListGridToString(llmAINavalShip, wavesOnTheGrid, templatePrompt.includeEmptySpaces));

            template = ReplaceTagWithText(template, "grid_overview_symbolic",
                ListSymbolicGridToString(llmAINavalShip, grid));

            return template;
        }

        private static string ReplaceTagWithText(string template, string tag, string text)
        {
            return template.Replace($"@{tag}@", text);
        }

        private static string ListSymbolicGridToString(LlmAINavalShip selfShip, List<GridUnit> gridUnits)
        {
            if (gridUnits == null || gridUnits.Count == 0)
            {
                return "Nothing";
            }

            var text = "\r\n";
            // ReSharper disable once ForeachCanBeConvertedToQueryUsingAnotherGetEnumerator
            foreach (var gridUnit in gridUnits)
            {
                var index = gridUnit.Index();
                if (!gridUnit.IsEmpty())
                {
                    var topActor = gridUnit.GetActor();
                    text += $"{index} = ";
                    switch (topActor)
                    {
                        case LlmAINavalShip llmAINavalShip:
                        {
                            if (!llmAINavalShip.Equals(selfShip))
                            {
                                text += GetOtherShipSymbolicText(selfShip, llmAINavalShip, text);
                            }
                            else
                            {
                                text += "🚢 self\r\n";
                            }

                            break;
                        }
                        case AIBaseShip aiBaseShip:
                        {
                            text += GetOtherShipSymbolicText(selfShip, aiBaseShip, text);
                            break;
                        }
                        case NavalShip navalShip:
                        {
                            text += GetOtherShipSymbolicText(selfShip, navalShip, text);
                            break;
                        }
                        case WaveActor wave:
                            text +=
                                $"{GridMoveTypeExtensions.GridMovementSymbol(wave.GetWaveDirection)}\r\n";
                            break;
                        case NavalTarget:
                            text += "= 🎯\r\n";
                            break;
                    }
                }
            }

            return text;

            string GetOtherShipSymbolicText(LlmAINavalShip selfLlmShip, NavalShip navalShip, string symbolicText)
            {
                var opposingFaction = !selfLlmShip.GetFaction().Equals(navalShip.GetFaction());
                var factionText = opposingFaction ? $"**Enemy**" : "**Ally**";
                var shipName = "";
                if (navalShip is AIBaseShip aiBaseShip)
                {
                    shipName = $"{aiBaseShip.GetAIName()} ";
                }

                symbolicText +=
                    $"🚢 {shipName}{factionText} health:{navalShip.GetCurrentHealth()}/{navalShip.GetMaxHealth()}\n";
                return symbolicText;
            }
        }

        //TODO unify ListGridToString and ListGridActorsIndicesToString
        private static string ListGridToString(NavalShip selfShip, List<GridUnit> gridUnits,
            bool includeEmpty = true)
        {
            if (gridUnits == null || gridUnits.Count == 0)
            {
                return "Nothing";
            }

            var text = "\r\n";
            // ReSharper disable once ForeachCanBeConvertedToQueryUsingAnotherGetEnumerator
            foreach (var gridUnit in gridUnits)
            {
                //(Format: [x,y] = content, where content can be: SHIP_faction_health_ratio, TARGET_health, WAVE_direction, or EMPTY)
                var index = gridUnit.Index();
                if (gridUnit.IsEmpty() && includeEmpty)
                {
                    text += $"{index} = EMPTY\r\n";
                }
                else
                {
                    var topActor = gridUnit.GetActor();
                    switch (topActor)
                    {
                        case LlmAINavalShip llmAINavalShip:
                        {
                            if (llmAINavalShip.Equals(selfShip))
                            {
                                text += $"{index} = SELF\r\n";
                            }
                            else
                            {
                                text += GetOtherShipGridToString(selfShip, llmAINavalShip, $"{index} = ");
                            }

                            break;
                        }
                        case AIBaseShip aiBaseShip:
                            text += GetOtherShipGridToString(selfShip, aiBaseShip, $"{index} = ");
                            break;
                        case NavalShip navalShip:
                            text += GetOtherShipGridToString(selfShip, navalShip, $"{index} = ");
                            break;
                        case WaveActor wave:
                            text +=
                                $"{index} = {GridMoveTypeExtensions.GridMovementSymbol(wave.GetWaveDirection)}\r\n";
                            break;
                        case NavalTarget target:
                            text += $"{index} = {GetTargetLine(target)}\r\n";
                            break;
                    }
                }
            }

            return text;

            string GetOtherShipGridToString(NavalShip selfNavalShip, NavalShip otherNavalShip, string symbolicText)
            {
                var opposingFaction = !selfNavalShip.GetFaction().Equals(otherNavalShip.GetFaction());
                var factionText = opposingFaction ? $"Enemy {otherNavalShip.GetFaction()}" : "Ally";
                if (otherNavalShip is AIBaseShip aiBaseShip)
                {
                    symbolicText += $"{GetAIBaseShipLine(aiBaseShip, factionText)}\r\n";
                }
                else
                {
                    symbolicText +=
                        $"🚢 {factionText} health:{otherNavalShip.GetCurrentHealth()}/{otherNavalShip.GetMaxHealth()}";
                }

                return symbolicText;
            }
        }

        private static string ListEnemyFactions(List<Faction> enemyFactions)
        {
            var text = "";
            // ReSharper disable once ForeachCanBeConvertedToQueryUsingAnotherGetEnumerator
            foreach (var aiFaction in enemyFactions)
            {
                text += $"{aiFaction},";
            }

            return text[..^1];
        }

        private static string ListGridUnitsToString(List<GridUnit> gridUnits, bool includeEmpty = true)
        {
            if (gridUnits == null || gridUnits.Count == 0)
            {
                return "[Nothing]";
            }

            var text = "[";
            // ReSharper disable once ForeachCanBeConvertedToQueryUsingAnotherGetEnumerator
            foreach (var gridUnit in gridUnits)
            {
                if (!gridUnit.IsEmpty() || (gridUnit.IsEmpty() && includeEmpty))
                {
                    text += $"{gridUnit.GetStringInfo()},";
                }
            }

            return text[..^1] + "]";
        }

        private static string ListGridUnitListActorsPairToString(List<GridUnitListActorsPair> list, Faction selfFaction,
            string separator = ", ")
        {
            if (list == null || list.Count == 0)
            {
                return "Nothing";
            }

            var text = "";
            foreach (var gridUnitListActorsPair in list)
            {
                var attackable = gridUnitListActorsPair.Two;
                var shouldInclude = false;
                var line = $"{gridUnitListActorsPair.One.Index()} → ";
                foreach (var actor in attackable)
                {
                    switch (actor)
                    {
                        case NavalTarget navalTarget:
                            line += $"{navalTarget.GetUnit().Index()} {GetTargetLine(navalTarget)}{separator}, ";
                            shouldInclude = true;
                            break;
                        case AIBaseShip aiBaseShip:
                            if (!selfFaction.Equals(aiBaseShip.GetFaction()))
                            {
                                line += $"{aiBaseShip.GetUnit().Index()} {GetAIBaseShipLine(aiBaseShip, aiBaseShip.GetFaction().ToString())}, ";
                                shouldInclude = true;
                            }
                            //If it is the from the same faction, then do not include.
                            break;
                        case WaveActor wave:
                            line += $"{wave.GetUnit().Index()} {GridMoveTypeExtensions.GridMovementSymbol(wave.GetWaveDirection)}, ";
                            shouldInclude = true;
                            break;
                        default: shouldInclude = false;
                            break;
                    }
                }

                if (!shouldInclude) continue;
                if (line.Length > 0)
                {
                    //Remove the last ", " from the line
                    line = line[..^2];
                }

                text += $"{line}{separator}";
            }

            return text;
        }

        private static string ListGridUnitIndicesToString(List<GridUnit> gridUnits, bool includeEmpty = true,
            string separator = ",")
        {
            if (gridUnits == null || gridUnits.Count == 0)
            {
                return "Nothing";
            }

            var text = "\r\n";
            // ReSharper disable once ForeachCanBeConvertedToQueryUsingAnotherGetEnumerator
            foreach (var gridUnit in gridUnits)
            {
                if (!gridUnit.IsEmpty() || (gridUnit.IsEmpty() && includeEmpty))
                {
                    text += $"{gridUnit.Index()}{separator}";
                }
            }

            return separator.Equals(",") ? text[..^1] : text + "\r\n";
        }

        private static string ListGridActorsIndicesToString(List<GridActor> gridActors, Faction selfFaction,
            bool fullInfo = false,
            string separator = ",")
        {
            if (gridActors == null || gridActors.Count == 0)
            {
                return "[Nothing]";
            }

            var text = "\r\n";
            var jumpLine = false;
            var dataAdded = false;
            // ReSharper disable once ForeachCanBeConvertedToQueryUsingAnotherGetEnumerator
            foreach (var gridActor in gridActors)
            {
                if (gridActor == null || gridActor.GetUnit() == null) continue;
                var index = gridActor.GetUnit().Index();
                if (fullInfo)
                {
                    switch (gridActor)
                    {
                        case NavalTarget navalTarget:
                            text += $"{index} = {GetTargetLine(navalTarget)}{separator}";
                            jumpLine = true;
                            dataAdded = true;
                            break;
                        case AIBaseShip aiBaseShip:
                        {
                            var opposingFaction = !selfFaction.Equals(aiBaseShip.GetFaction());
                            var factionText = opposingFaction ? $"Enemy {aiBaseShip.GetFaction()}" : "Ally";
                            text += $"{index} = {GetAIBaseShipLine(aiBaseShip, factionText)}";
                            jumpLine = true;
                            dataAdded = true;
                        }
                            break;
                        case WaveActor wave:
                            text +=
                                $"{index} = {GridMoveTypeExtensions.GridMovementSymbol(wave.GetWaveDirection)}{separator}";
                            jumpLine = true;
                            dataAdded = true;
                            break;
                    }

                    if (jumpLine)
                    {
                        text += $"{separator}";
                    }

                    jumpLine = false;
                }
                else
                {
                    text += $"{gridActor.GetUnit().Index()}{separator}";
                    dataAdded = true;
                }
            }

            if (dataAdded)
                return separator.Equals(",") ? text[..^1] : text + "\r\n";
            return "[Nothing]";
        }

        private static string GetWaveLine(WaveActor wave, bool hasWaveTargets = false)
        {
            var waveLine = $"{GridMoveTypeExtensions.GridMovementSymbol(wave.GetWaveDirection)}";
            if (hasWaveTargets)
            {
                var unitsAffectedByWaveAttack = wave.GetUnitsAffectedByWaveAttack();
                // var actorsAffectedByWaveAttack = unitsAffectedByWaveAttack.FindAll(u => !u.IsEmpty());
                var actorsAffectedByWaveAttack = unitsAffectedByWaveAttack.Select(u => u.GetActor()).ToList().FindAll(a => a != null);
                if (actorsAffectedByWaveAttack.Count == 0)
                {
                    waveLine += "[Nothing]";
                }
                else
                {
                    
                }
            }
            
            return waveLine;
        }
        
        private static string GetAIBaseShipLine(AIBaseShip aiBaseShip, string factionText)
        {
            return
                $"🚢 {aiBaseShip.GetAIName()} {factionText} health:{aiBaseShip.GetCurrentHealth()}/{aiBaseShip.GetMaxHealth()}";
        }

        private static string GetTargetLine(NavalTarget navalTarget)
        {
            return $"🎯 health:{navalTarget.GetCurrentHealth()}/{navalTarget.GetMaxHealth()}";
        }
    }
}