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

namespace Actors.AI.Brain
{
    [CreateAssetMenu(fileName = "New Classic Utility AI Brain", menuName = "Waves/AI/Classic Utility AI Brain", order = 2)]
    public class AIClassicUtilityBrainMachine : AIBrainMachine
    {
        private AIClassicState _state = AIClassicState.Start;
        
        public override void StartTurn(AINavalShip aiNavalShip)
        {
            _state = AIClassicState.Start;
        }

        public override bool CalculateMovement(AINavalShip aiNavalShip, int stepsAvailable, bool afterAttack, out AIGridUnitUtility moveTo)
        {
            var walkableUnits = AIBaseShip.GetFilteredWalkableTiles(aiNavalShip, stepsAvailable); 
            
            var utilities = new List<AIGridUnitUtility>();
            var genes = aiNavalShip.GetGenesData();
            var cannonData = aiNavalShip.NavalCannon.GetCannonSo;
            var gridManager = GridManager.GetSingleton();
            var allEnemies = gridManager.GetAllGridActorsOf<NavalShip>(gridActor => gridActor is NavalShip ship && !ship.GetFaction().Equals(aiNavalShip.GetFaction()));
            
            //First calculate all possible movements
            foreach (var unit in walkableUnits)
            {
                var gridUnitUtility = new AIGridUnitUtility(unit);
                //Calculate the utility of moving to a given position
                var movementUtility = AIGridUnitUtility.CalculateUtilityToMoveToGridUnit(aiNavalShip, unit);
                var awarenessUtility = 0.0f;
                var attackUtility = 0.0f;

                //Then calculate the utility of the surroundings of the given position
                var awarenessRadius = Mathf.FloorToInt(genes.awareness);
                
                if (awarenessRadius >= 1)
                {
                    var awarenessUnits = gridManager
                        .GetGridUnitsInRadiusManhattan(unit.Index(), awarenessRadius);
                    //Remove self position
                    awarenessUnits.Remove(unit);
                    awarenessUnits.ForEach(awarenessUnit =>
                    {
                        awarenessUtility +=
                            AIGridUnitUtility.CalculateProximityUtility(aiNavalShip, awarenessUnit, allEnemies);
                    });
                }

                //Finally, calculate the utilities of attacking from this position
                var attackableTilesFromUnit = gridManager.GetGridUnitsForMoveType(cannonData.targetAreaType,
                    unit.Index(), cannonData.area, cannonData.deadZone);
                if (attackableTilesFromUnit.Count >= 0)
                {
                    //Remove self position
                    attackableTilesFromUnit.Remove(unit);
                    attackableTilesFromUnit.ForEach(attackableTile =>
                    {
                        if (afterAttack)
                        {
                            attackUtility += AIGridUnitUtility.CalculatePossibilityOfBeingAttackedUtility(aiNavalShip, attackableTile);
                        }
                        else
                        {
                            attackUtility += AIGridUnitUtility.CalculatePossibleAttackUtility(aiNavalShip, attackableTile);    
                        }
                    });
                }

                var possibleWaveTargetUtility = 0.0f;
                var crossTiles = gridManager
                    .GetGridUnitsForMoveType(GridMoveType.Cross, unit.Index(), 1);
                foreach (var crossTile in crossTiles)
                {
                    if (!crossTile.GetFirstActorOfType<WaveActor>(out var waveActor)) continue;
                    if (!waveActor.GetUnitsAffectedByWaveAttack().Contains(unit)) continue;
                    //This wave can be used to target this tile
                    possibleWaveTargetUtility -= waveActor.GetDamage();

                    var possibleAttackersFromWave = gridManager.GetGridUnitsForMoveType(
                        cannonData.targetAreaType,
                        crossTile.Index(), cannonData.area, cannonData.deadZone);
                    var enemies = possibleAttackersFromWave.FindAll(targetedUnit => targetedUnit.GetFirstActorOfType<NavalShip>(out var ship) && !ship.GetFaction().Equals(aiNavalShip.GetFaction()));
                    enemies.ForEach(enemyGridUnit =>
                    {
                        possibleWaveTargetUtility +=
                            AIGridUnitUtility.CalculatePossibilityOfBeingAttackedUtility(aiNavalShip,
                                enemyGridUnit);
                    });
                }

                var resultingUtility = movementUtility + awarenessUtility + attackUtility + possibleWaveTargetUtility;
                AIGridUnitUtility.CapLowestBoundUtility(ref resultingUtility);
                gridUnitUtility.Utility = resultingUtility;
                utilities.Add(gridUnitUtility);
            }
            
            DisplayHeatMap(utilities);

            AIGridUnitUtility chosenAction = null;
            var best = PickBestUtility(aiNavalShip, ref chosenAction, utilities);
            moveTo = chosenAction;
            return best;
        }

        public override bool CalculateAttack(AINavalShip aiNavalShip, out AIGridUnitUtility attack)
        {
            attack = null;
            var position = aiNavalShip.GetUnit().Index();
            var cannonData = aiNavalShip.NavalCannon.GetCannonSo;
            var attackableFromUnit = GridManager.GetSingleton().GetGridUnitsForMoveType(cannonData.targetAreaType,
                position, cannonData.area, cannonData.deadZone);

            if (attackableFromUnit == null || attackableFromUnit.Count == 0) return false;

            var utilities = new List<AIGridUnitUtility>();
            foreach (var unit in attackableFromUnit)
            {
                var gridUnitUtility = new AIGridUnitUtility(unit);
                var utility = AIGridUnitUtility.CalculateAttackUtility(aiNavalShip, unit);
                if (Mathf.Approximately(utility, AIGridUnitUtility.GetLowestUtility())) continue;
                gridUnitUtility.Utility = utility;
                var gridUnit = gridUnitUtility.GetUnit();
                if (gridUnit.HasValidActors() && gridUnit.ActorsCount() > 0)
                {
                    utilities.Add(gridUnitUtility);    
                }
            }
            
            AIGridUnitUtility chosenAction = null;
            if (utilities.Count <= 0) return false;
            DisplayHeatMap(utilities);
            
            var best = PickBestUtility(aiNavalShip, ref chosenAction, utilities);
            attack = chosenAction;
            return best;
        }

        public override AIAction CalculateAction(AINavalShip aiNavalShip, int actionsAvailable, int stepsAvailable, bool afterAttack,
            out List<AIGridUnitUtility> targets)
        {
            targets = new List<AIGridUnitUtility>();
            
            switch (_state)
            {
                case AIClassicState.Start: //Start by moving
                    _state = AIClassicState.Move;
                    CalculateMovement(aiNavalShip, stepsAvailable, false, out var target);
                    targets.Add(target);
                    return AIAction.Movement;
                
                case AIClassicState.Move: //Attack after moving
                    _state = AIClassicState.Attack;
                    var attack = CalculateAttack(aiNavalShip, out target);
                    if (attack && target != null)
                    {
                        targets.Add(target);
                    }
                    return attack ? AIAction.Attack : AIAction.None;
                case AIClassicState.Attack:
                    _state = AIClassicState.MoveAgain; //Move again after attacking, if possible
                    //TODO change this to calculate a movement after attack
                    if (stepsAvailable > 0 && CalculateMovement(aiNavalShip, stepsAvailable, true, out target))
                    {
                        targets.Add(target);
                        return AIAction.Movement;
                    }
                    //Else, moves to the end of the method and return AIAction.EndTurn
                    break;
                case AIClassicState.MoveAgain:
                default:
                    break;
            }
            
            return AIAction.EndTurn;
        }
        
        public override string ToString()
        {
            return $"C{base.ToString()}";
        }
    }
}