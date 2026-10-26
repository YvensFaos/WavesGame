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
        private AIClassicState _state = AIClassicState.Start;
        
        public override void StartTurn(AINavalShip aiNavalShip)
        {
            _state = AIClassicState.Start;
        }

        private List<GridUnit> PlanMovementRoute(AINavalShip aiNavalShip, int stepsAvailable, bool canAttack, out GridUnit unitToAttack)
        {
            var cannonData = aiNavalShip.NavalCannon.GetCannonSo;
            var genes = aiNavalShip.GetGenesData();
            var path = new List<GridUnit>();
            unitToAttack = null;
            
            //If can attack, then it is in aggressive mode; if it cannot attack, then it is in retreat mode.
            if (canAttack)
            {
                var currentUnit = aiNavalShip.GetUnit();
                var shouldAttack = false;
                while(!shouldAttack && path.Count < stepsAvailable)
                {
                    shouldAttack = CanAttackImmediately(aiNavalShip, currentUnit, out var target);
                    if (shouldAttack)
                    {
                        unitToAttack = target.GetUnit();
                        
                    }
                    else
                    {
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
                            //TODO return from here with the path
                        }
                    }
                    // else
                    // {
                    //     //Found a good target from the current position
                    //     
                    //     return null;
                    // }
                }
            }

            //TODO
            return null;
        }

        public override bool CalculateMovement(AINavalShip aiNavalShip, int stepsAvailable, bool afterAttack,
            out AIGridUnitUtility moveTo)
        {
            //TODO change 
            
            var position = aiNavalShip.GetUnit().Index();
            var walkableUnits = GridManager.GetSingleton().GetGridUnitsInRadiusManhattan(position, stepsAvailable);
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
            out AIGridUnitUtility target)
        {
            target = null;

            switch (_state)
            {
                // Always start the turn by trying to attack closer enemies
                case AIClassicState.Start:
                {
                    if (CanAttackImmediately(aiNavalShip, aiNavalShip.GetUnit(), out var attackTarget))
                    {
                        _state = AIClassicState.Attack;
                        target = attackTarget;
                        return AIAction.Attack;
                    }
                    // If it can't, then proceed to calculate movement.
                    _state = AIClassicState.Move;
                    
                    //TODO change
                    CalculateMovement(aiNavalShip, stepsAvailable, false, out target);
                    return AIAction.Movement;
                }
                    break;
                case AIClassicState.Move:
                    break;
                case AIClassicState.Attack:
                    break;
                case AIClassicState.MoveAgain:
                    break;
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