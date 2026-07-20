using System.Collections.Generic;
using Grid;
using UnityEngine;
using UUtils;

namespace Actors.AI.Brain
{
    [CreateAssetMenu(fileName = "New Utility AI Brain", menuName = "Waves/AI/Utility AI Brain", order = 1)]
    public class AIUtilityBrainMachine : AIBrainMachine
    {
        public override bool CalculateMovement(AINavalShip aiNavalShip, int stepsAvailable,
            out AIGridUnitUtility moveTo)
        {
            var position = aiNavalShip.GetUnit();
            var positionIndex = position.Index();
            var cannonData = aiNavalShip.NavalCannon.GetCannonSo;
            var walkableUnits = GridManager.GetSingleton().GetGridUnitsInRadiusManhattan(positionIndex, stepsAvailable);
            var utilities = new List<AIGridUnitUtility>();
            var genes = aiNavalShip.GetGenesData();

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
            var position = aiNavalShip.GetUnit();
            var positionIndex = position.Index();
            var cannonData = aiNavalShip.NavalCannon.GetCannonSo;
            var attackableFromUnit = GridManager.GetSingleton().GetGridUnitsForMoveType(cannonData.targetAreaType,
                positionIndex, cannonData.area, cannonData.deadZone);
            attack = null;

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
            if (actionsAvailable <= 0 && stepsAvailable <= 0) return AIAction.EndTurn;
            AIGridUnitUtility moveTo = null;
            AIGridUnitUtility attackAt = null;

            var shouldMove = stepsAvailable > 0;
            if (shouldMove)
            {
                shouldMove = CalculateMovement(aiNavalShip, stepsAvailable, out moveTo);
                if (moveTo.GetUnit().Equals(aiNavalShip.GetUnit()))
                {
                    //Trying to move to the same position
                    shouldMove = false;
                }
            }
            var shouldAttack = actionsAvailable > 0;
            if (shouldAttack) shouldAttack = CalculateAttack(aiNavalShip, out attackAt);

            switch (shouldMove)
            {
                //If should not move and should not attack
                case false when !shouldAttack:
                    return AIAction.None;
                //If should move and should attack, then use the one with the highest utility
                case true when shouldAttack:
                {
                    var action = moveTo.Utility > attackAt.Utility ? AIAction.Movement : AIAction.EndTurn;
                    target = moveTo.Utility > attackAt.Utility ? moveTo : attackAt;
                    return action;
                }
                //If should only move and not attack
                case true:
                    target = moveTo;
                    return AIAction.Movement;
                //If should not move but should attack
                default:
                    target = attackAt;
                    return AIAction.Attack;
            }
        }

        private static bool PickBestUtility(AINavalShip aiNavalShip, ref AIGridUnitUtility chosenAction,
            List<AIGridUnitUtility> utilities)
        {
            //TODO transform this into a reusable function
            if (utilities.Count == 0) return false;
            var aiGenesSo = aiNavalShip.GetGenesData();
            if (aiGenesSo.sortUtilities)
            {
                utilities.Sort();
            }

            var possibleActionsCount = Mathf.Min(utilities.Count, aiGenesSo.possibleActionsCount);
            var possibleActions = utilities.GetRange(0, possibleActionsCount);
            if (aiGenesSo.doubleBestUtilityChance)
            {
                //Add the highest utility again on the list to improve its odds
                possibleActions.Add(possibleActions[0]);
            }

            chosenAction = RandomHelper<AIGridUnitUtility>.GetRandomFromListWithIndex(possibleActions, out var index);
            DebugUtilityChoices(chosenAction, index, utilities);
            return true;
        }

        private static void DebugUtilityChoices(AIGridUnitUtility chosenAction, int index,
            List<AIGridUnitUtility> utilities)
        {
            //TODO block this when building
            DebugUtils.DebugLogMsg($"Action {index}/{utilities.Count}: {chosenAction} chosen.",
                DebugUtils.DebugType.Regular);
            for (var i = 0; i < Mathf.Min(5, utilities.Count); i++)
            {
                DebugUtils.DebugLogMsg($"Utils => {i} {utilities[i]}", DebugUtils.DebugType.Verbose);
            }
        }

        public override string ToString()
        {
            return "AIUtilityBrainMachine";
        }
    }
}