using System;
using System.Collections.Generic;
using System.Linq;
using Actors.Cannon;
using Grid;
using UnityEngine;
using UUtils;

namespace Actors.AI.Brain
{
    [CreateAssetMenu(fileName = "New Hyper Utility AI Brain", menuName = "Waves/AI/Hyper Utility AI Brain", order = 3)]
    public class AIHyperBrainMachine : AIBrainMachine
    {
        private AIHyperState _state = AIHyperState.Start;
        private List<GridUnit> _route = new List<GridUnit>();
        private GridUnit _plannedTarget;
        
        public override void StartTurn(AINavalShip aiNavalShip)
        {
            _state = AIHyperState.Start;
        }

        private List<GridUnit> PlanTurn(AINavalShip aiNavalShip, int stepsAvailable, bool canAttack,
            out GridUnit unitToAttack)
        {
            var faction =  aiNavalShip.GetFaction();
            var allEnemies = GridManager.GetSingleton().GetAllGridActorsOf<NavalShip>(actor => actor is NavalShip navalShip && !navalShip.GetFaction().Equals(faction));
            
            var position = aiNavalShip.GetUnit().Index();
            var walkableUnits = GridManager.GetSingleton().GetGridUnitsInRadiusManhattan(position, stepsAvailable);

            var wavesInPath = new List<WaveActor>();
            walkableUnits.ForEach(unit =>
            {
                if (unit.GetFirstActorOfType<WaveActor>(out var waveActor))
                {
                    wavesInPath.Add(waveActor);
                }
            });
            if (wavesInPath.Count > 0)
            {
                foreach (var waveActor in wavesInPath)
                {
                    var closeTilesToTheWave = GridManager.GetSingleton().GetGridUnitsForMoveType(GridMoveType.Cross, waveActor.GetUnit().Index(), 1);
                    
                    // var affectedUnits = waveActor.GetUnitsAffectedByWaveAttack();
                    // foreach (var affectedUnit in affectedUnits)
                    // {
                    //     
                    // }
                }
            }
            
            unitToAttack = null;
            return null;
        }

        private List<GridUnit> PlanMovementRoute(AINavalShip aiNavalShip, int stepsAvailable, bool canAttack, out GridUnit unitToAttack)
        {
            var cannonData = aiNavalShip.NavalCannon.GetCannonSo;
            var genes = aiNavalShip.GetGenesData();
            var path = new List<GridUnit>();
            unitToAttack = null;
            var currentUnit = aiNavalShip.GetUnit();
                
            //If can attack, then it is in aggressive mode; if it cannot attack, then it is in retreat mode.
            if (canAttack)
            {
                while(path.Count < stepsAvailable)
                {
                    var shouldAttack = CanAttackImmediately(aiNavalShip, currentUnit, out var target);
                    if (shouldAttack)
                    {
                        unitToAttack = target.GetUnit();
                        break;
                    }
                    var walkableUnits = GridManager.GetSingleton().
                        GetGridUnitsInRadiusManhattan(currentUnit.Index(), 1);
                    var utilities = walkableUnits.Select(gridUnit => CalculateTotalUtilityToMoveToGridUnit(aiNavalShip, false, gridUnit, genes, cannonData)).ToList();
                    DisplayHeatMap(utilities);
                    AIGridUnitUtility chosenAction = null;
                    var hasBestOption = PickBestUtility(aiNavalShip, ref chosenAction, utilities);
                    if (hasBestOption)
                    {
                        var chosenUnit = chosenAction.GetUnit();
                        path.Add(chosenUnit);
                        currentUnit = chosenUnit;
                    }
                    else
                    {
                        DebugUtils.DebugLogErrorMsg($"Failed to find a best utility position from {currentUnit.Index()}!");
                        break;
                    }
                }
            }
            else
            {
                while (path.Count < stepsAvailable)
                {
                    var walkableUnits = GridManager.GetSingleton().
                        GetGridUnitsInRadiusManhattan(currentUnit.Index(), 1);
                    var utilities = walkableUnits.Select(gridUnit => CalculateTotalUtilityToMoveToGridUnit(aiNavalShip, false, gridUnit, genes, cannonData)).ToList();
                    var utilityStaying =
                        CalculateTotalUtilityToMoveToGridUnit(aiNavalShip, false, currentUnit, genes, cannonData);
                    utilities.Add(utilityStaying);
                    DisplayHeatMap(utilities);
                    AIGridUnitUtility chosenAction = null;
                    var hasBestOption = PickBestUtility(aiNavalShip, ref chosenAction, utilities);
                    if (hasBestOption)
                    {
                        var chosenUnit = chosenAction.GetUnit();
                        path.Add(chosenUnit);
                        if (chosenUnit.Equals(currentUnit))
                        {
                            //Staying in the same position
                            break;
                        }
                        currentUnit = chosenUnit;
                    }
                    else
                    {
                        DebugUtils.DebugLogErrorMsg($"Failed to find a best utility position from {currentUnit.Index()}!");
                        break;
                    }
                }
            }

            return path;
        }

        public override bool CalculateMovement(AINavalShip aiNavalShip, int stepsAvailable, bool afterAttack,
            out AIGridUnitUtility moveTo)
        {
            //TODO change 
            
            var walkableUnits = AIBaseShip.GetFilteredWalkableTiles(aiNavalShip, stepsAvailable); 
            var genes = aiNavalShip.GetGenesData();
            var cannonData = aiNavalShip.NavalCannon.GetCannonSo;
            
            //First calculate all possible movements
            var utilities = walkableUnits.Select(unit => CalculateTotalUtilityToMoveToGridUnit(aiNavalShip, afterAttack, unit, genes, cannonData)).ToList();
            DisplayHeatMap(utilities);

            AIGridUnitUtility chosenAction = null;
            var best = PickBestUtility(aiNavalShip, ref chosenAction, utilities);
            moveTo = chosenAction;
            return best;
        }

        private static AIGridUnitUtility CalculateTotalUtilityToMoveToGridUnit(AINavalShip aiNavalShip, bool afterAttack, GridUnit unit,
            AIGenesSO genes, CannonSo cannonData)
        {
            //TODO consider calculating the wave utility based on the utility of the position you will move AFTER being pushed
            
            var gridUnitUtility = new AIGridUnitUtility(unit);
            var movementUtility = AIGridUnitUtility.CalculateUtilityToMoveToGridUnit(aiNavalShip, unit);
            var awarenessUtility = 0.0f;
            var attackUtility = 0.0f;

            //Then calculate the utility of the surroundings of the given position
            var awarenessRadius = Mathf.FloorToInt(genes.awareness);
            if (awarenessRadius >= 1)
            {
                var awarenessUnits = GridManager.GetSingleton()
                    .GetGridUnitsInRadiusManhattan(unit.Index(), awarenessRadius);
                //Remove self position
                awarenessUnits.Remove(unit);
                awarenessUnits.ForEach(awarenessUnit =>
                {
                    awarenessUtility +=
                        AIGridUnitUtility.CalculateProximityUtility(aiNavalShip, awarenessUnit);
                });
            }

            //Finally, calculate the utilities of attacking from this position
            var attackableTilesFromUnit = GridManager.GetSingleton().GetGridUnitsForMoveType(cannonData.targetAreaType,
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

            var resultingUtility = movementUtility + awarenessUtility + attackUtility;
            AIGridUnitUtility.CapLowestBoundUtility(ref resultingUtility);
            gridUnitUtility.Utility = resultingUtility;
            return gridUnitUtility;
        }

        public override bool CalculateAttack(AINavalShip aiNavalShip, out AIGridUnitUtility attack)
        {
            throw new System.NotImplementedException();
        }

        public override AIAction CalculateAction(AINavalShip aiNavalShip, int actionsAvailable, int stepsAvailable,
            bool afterAttack,
            out List<AIGridUnitUtility> targets)
        {
            targets = new List<AIGridUnitUtility>();

            switch (_state)
            {
                // Always start the turn by trying to attack closer enemies
                case AIHyperState.Start:
                {
                    if (CanAttackImmediately(aiNavalShip, aiNavalShip.GetUnit(), out var attackTarget))
                    {
                        _state = AIHyperState.Attack;
                        targets.Add(attackTarget);
                        return AIAction.Attack;
                    }
                    
                    // If it can't, then proceed to calculate movement.
                    _route = PlanMovementRoute(aiNavalShip, stepsAvailable, true, out var unitToAttack);
                    if (_route.Count <= 0) return AIAction.EndTurn;
                    //There is a path to walk to
                    targets.AddRange(_route.Select(routeStep => new AIGridUnitUtility(routeStep)));
                    if (attackTarget != null)
                    {
                        //There is a planned target to be attacked
                        _plannedTarget = attackTarget.GetUnit();
                        _state = AIHyperState.MoveAndAttack;
                    }
                    else
                    {
                        //There is no planned target to be attacked, then just proceed to move
                        _state = AIHyperState.Move;
                    }
                    return AIAction.Movement;
                }
                case AIHyperState.Move:
                    break;
                case AIHyperState.Attack:
                    break;
                case AIHyperState.MoveAgain:
                    break;
                case AIHyperState.Moving:
                case AIHyperState.MoveAndAttack:
                case AIHyperState.MovingAgain:
                default:
                    throw new ArgumentOutOfRangeException();
            }

            return AIAction.None;
        }

        private bool CanAttackImmediately(AINavalShip aiNavalShip, GridUnit fromPosition, out AIGridUnitUtility attack)
        {
            var positionIndex = fromPosition.Index();
            var cannonData = aiNavalShip.NavalCannon.GetCannonSo;
            var attackableFromUnit = GridManager.GetSingleton().GetGridUnitsForMoveType(cannonData.targetAreaType,
                positionIndex, cannonData.area, cannonData.deadZone);
            attack = null;

            var utilities = new List<AIGridUnitUtility>();
            var shouldAttackImmediately = false;

            if (attackableFromUnit == null || attackableFromUnit.Count == 0) return false;

            foreach (var unit in attackableFromUnit)
            {
                //Skip naval targets if they are forbidden from being targeted
                if (aiNavalShip.GenesData.forbidTargetAttacking && unit.HasValidActors() &&
                    unit.GetActor() is NavalTarget)
                {
                    continue;
                }

                var actor = unit.GetActor();
                switch (actor)
                {
                    case NavalShip navalShip:
                        var faction = navalShip.GetFaction();
                        if (faction != aiNavalShip.GetFaction())
                        {
                            shouldAttackImmediately = CalculateUtilityForUnit(unit);
                        }

                        break;
                    case WaveActor waveActor:
                        var affectedByWave = waveActor.GetUnitsAffectedByWaveAttack();
                        if (affectedByWave is { Count: > 0 })
                        {
                            var shouldAttack = true;
                            foreach (var gridUnit in affectedByWave)
                            {
                                if (gridUnit.HasValidActors() && gridUnit.GetActor() is NavalShip navalShip)
                                {
                                    shouldAttack &= navalShip.GetFaction() != aiNavalShip.GetFaction();
                                }
                            }
                            if (shouldAttack)
                            {
                                shouldAttackImmediately = CalculateUtilityForUnit(unit);
                            }
                        }

                        break;
                }
            }

            if (!shouldAttackImmediately) return false;
            if (utilities.Count == 0) return false;
            DisplayHeatMap(utilities);

            AIGridUnitUtility chosenAction = null;
            var best = PickBestUtility(aiNavalShip, ref chosenAction, utilities);
            attack = chosenAction;
            return best;

            bool CalculateUtilityForUnit(GridUnit unit)
            {
                var utility = AIGridUnitUtility.CalculateAttackUtility(aiNavalShip, unit);
                if (Mathf.Approximately(utility, float.MinValue)) return shouldAttackImmediately;
                utilities.Add(new AIGridUnitUtility(unit, utility));
                shouldAttackImmediately = true;
                return true;
            }
        }

        public override string ToString()
        {
            return $"Hy{base.ToString()}";
        }
    }
}