/*
 * Copyright (c) 2026 Yvens R Serpa [https://github.com/YvensFaos/]
 *
 * This work is licensed under the Creative Commons Attribution 4.0 International License.
 * To view a copy of this license, visit http://creativecommons.org/licenses/by/4.0/
 * or see the LICENSE file in the root directory of this repository.
 */

using System.Collections.Generic;
using Grid;
using UnityEngine;

namespace Actors.AI.Brain
{
    internal enum AIClassicState
    {
        Start, Move, Attack, MoveAgain
    }
    
    [CreateAssetMenu(fileName = "New Classic Utility AI Brain", menuName = "Waves/AI/Classic Utility AI Brain", order = 2)]
    public class AIClassicUtilityBrainMachine : AIBrainMachine
    {
        private AIClassicState _state = AIClassicState.Start;
        
        public override void StartTurn(AINavalShip aiNavalShip)
        {
            _state = AIClassicState.Start;
        }

        public override bool CalculateMovement(AINavalShip aiNavalShip, int stepsAvailable, out AIGridUnitUtility moveTo)
        {
            var position = aiNavalShip.GetUnit().Index();
            var walkableUnits = GridManager.GetSingleton().GetGridUnitsInRadiusManhattan(position, stepsAvailable);
            var utilities = new List<AIGridUnitUtility>();
            var genes = aiNavalShip.GetGenesData();
            var cannonData = aiNavalShip.NavalCannon.GetCannonSo;
            
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
                var attackableFromUnit = GridManager.GetSingleton().GetGridUnitsForMoveType(cannonData.targetAreaType,
                    unit.Index(), cannonData.area, cannonData.deadZone);
                if (attackableFromUnit.Count >= 0)
                {
                    //Remove self position
                    attackableFromUnit.Remove(unit);
                    attackableFromUnit.ForEach(attackUnit =>
                    {
                        attackUtility += AIGridUnitUtility.CalculatePossibleAttackUtility(aiNavalShip, attackUnit);
                    });
                }

                gridUnitUtility.Utility = movementUtility + awarenessUtility + attackUtility;
                utilities.Add(gridUnitUtility);
            }

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
                if (Mathf.Approximately(utility, float.MinValue)) continue;
                gridUnitUtility.Utility = utility;
                utilities.Add(gridUnitUtility);
            }

            AIGridUnitUtility chosenAction = null;
            var best = PickBestUtility(aiNavalShip, ref chosenAction, utilities);
            attack = chosenAction;
            return best;
        }

        public override AIAction CalculateAction(AINavalShip aiNavalShip, int actionsAvailable, int stepsAvailable,
            out AIGridUnitUtility target)
        {
            target = null;
            
            switch (_state)
            {
                case AIClassicState.Start:
                    _state = AIClassicState.Move;
                    CalculateMovement(aiNavalShip, stepsAvailable, out target);
                    return AIAction.Movement;
                case AIClassicState.Move:
                    _state = AIClassicState.Attack;
                    return CalculateAttack(aiNavalShip, out target) ? AIAction.Attack : AIAction.None;
                case AIClassicState.Attack:
                    _state = AIClassicState.MoveAgain;
                    if (stepsAvailable > 0 && CalculateMovement(aiNavalShip, stepsAvailable, out target))
                    {
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
            return "AIClassicUtilityBrainMachine";
        }
    }
}