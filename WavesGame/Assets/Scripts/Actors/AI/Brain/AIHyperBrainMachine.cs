using System;
using System.Collections.Generic;
using Actors.AI.LlmAI;
using Grid;
using UnityEngine;

namespace Actors.AI.Brain
{
    [CreateAssetMenu(fileName = "New Hyper Utility AI Brain", menuName = "Waves/AI/Hyper Utility AI Brain", order = 3)]
    public class AIHyperBrainMachine : AIBrainMachine
    {
        public override void StartTurn(AINavalShip aiNavalShip)
        {
            // throw new System.NotImplementedException();
        }

        public override bool CalculateMovement(AINavalShip aiNavalShip, int stepsAvailable, bool afterAttack,
            out AIGridUnitUtility moveTo)
        {
            throw new System.NotImplementedException();
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
            var canMove = stepsAvailable > 0;
            var canAttack = actionsAvailable > 0;

            if (canAttack && CanAttackImmediately(aiNavalShip, out var attackTarget))
            {
                target = attackTarget;
                return AIAction.Attack;
            }

            if (canMove)
            {
                
            }
            

            return AIAction.None;
        }

        private bool CanAttackImmediately(AINavalShip aiNavalShip, out AIGridUnitUtility attack)
        {
            var position = aiNavalShip.GetUnit();
            var positionIndex = position.Index();
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