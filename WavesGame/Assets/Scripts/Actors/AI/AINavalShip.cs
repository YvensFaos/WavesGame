/*
 * Copyright (c) 2026 Yvens R Serpa [https://github.com/YvensFaos/]
 *
 * This work is licensed under the Creative Commons Attribution 4.0 International License.
 * To view a copy of this license, visit http://creativecommons.org/licenses/by/4.0/
 * or see the LICENSE file in the root directory of this repository.
 */

using System;
using System.Collections;
using System.Linq;
using Actors.AI.Brain;
using Core;
using UnityEngine;
using UUtils;

namespace Actors.AI
{
    public class AINavalShip : AIBaseShip
    {
        [field: SerializeField] public AIGenesSO GenesData { get; set; }

        [SerializeField] private AIBrainMachine brain;
        private bool _calculatingAction;

        protected override void Start()
        {
            base.Start();
            UpdateName();
        }

        protected override IEnumerator TurnAI()
        {
            yield return new WaitForEndOfFrame();

            var actionsLeft = ActionsLeft;
            var remainingSteps = RemainingSteps;
            DebugUtils.DebugLogMsg($"{name} has {actionsLeft} actions and steps {remainingSteps}!", DebugUtils.DebugType.System);
            brain.StartTurn(this);
            var attackedAlready = false;
            var cursorController = CursorController.GetSingleton();
            
            AIAction act;
            do
            {
                act = brain.CalculateAction(this, actionsLeft, remainingSteps, attackedAlready, out var targets);
                yield return new WaitForSeconds(2.0f);

                var targetString = "[No Target]";
                if (targets is { Count: > 0 })
                {
                    if (targets.Count == 1)
                    {
                        targetString = targets[0].ToString();
                    }
                    else
                    {
                        //There are multiple targets
                        targetString = targets.Aggregate(
                            "multiple targets: [", (current, target) => current + $"{target}, ");
                        targetString = targetString.Remove(targetString.Length - 2);
                        targetString += "]";
                    }
                }
                var utilityReasoning = $"{name} has selected action {act} with target {targetString}.";
                DebugUtils.DebugLogMsg($"{utilityReasoning}", DebugUtils.DebugType.System);
                
                switch (act)
                {
                    case AIAction.None:
                        DebugUtils.DebugLogMsg($"{name} decided on no action! Has still {actionsLeft} actions and {remainingSteps} movements left.", DebugUtils.DebugType.System);
                        act = AIAction.EndTurn;
                        goto case AIAction.EndTurn;
                    case AIAction.Movement:
                        DisplayMoveTiles(this, remainingSteps);
                        if (targets == null || targets.Count == 0)
                        {
                            DebugUtils.DebugLogErrorMsg($"Invalid target movement position [null].");
                            HideTiles();
                            break;
                        }
                        _calculatingAction = true;
                        foreach (var moveTo in targets.Select(target => target.GetUnit()))
                        {
                            yield return new WaitForSeconds(0.5f);
                            cursorController.MoveToIndex(moveTo.Index());
                            yield return new WaitUntil(() => !cursorController.MovingAnimation());
                            var move = MoveTo(targets[0].GetUnit(), _ =>
                            {
                                _calculatingAction = false;
                            }, true);
                            yield return new WaitUntil(() => !_calculatingAction);
                            remainingSteps = move ? RemainingSteps : 0;
                        }
                        HideTiles();
                        break;
                    
                    case AIAction.Attack:
                        DisplayAttackTiles(this);
                        if (targets == null || targets.Count == 0)
                        {
                            DebugUtils.DebugLogErrorMsg($"Invalid target movement position [null].");
                            break;
                        }
                        var targetUnit = targets[0].GetUnit();
                        if (targetUnit.ActorsCount() <= 0)
                        {
                            DebugUtils.DebugLogErrorMsg($"Target position has no valid targets [{targetUnit}].");
                            break;
                        }
                        var firstActor = targetUnit.GetActor();
                        DebugUtils.DebugLogMsg($"{name} attacks {firstActor}!", DebugUtils.DebugType.System);
                        if (TryToAct())
                        {
                            var attackAt = targets[0].GetUnit();
                            yield return new WaitForSeconds(0.5f);
                            cursorController.MoveToIndex(attackAt.Index());
                            attackedAlready = true;
                            yield return new WaitUntil(() => !cursorController.MovingAnimation());
                            
                            --actionsLeft;
                            var damage = CalculateDamage();
                            RecordAttack(firstActor, targetUnit, damage, utilityReasoning);
                            kills += targetUnit.DamageActors(damage);
                            yield return new WaitForSeconds(0.75f);
                        }
                        else
                        {
                            DebugUtils.DebugLogErrorMsg($"{name} cannot act! No more valid actions this turn.");
                        }
                        HideTiles();
                        break;
                    case AIAction.EndTurn:
                        DebugUtils.DebugLogMsg($"{name} finishes its turn!", DebugUtils.DebugType.System);
                        //TODO return the cursor to the unit
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            } while (act != AIAction.EndTurn);
            FinishAITurn();
        }

        public override void UpdateName()
        {
            var internalIDStr = internalID.ToString();
            var factionName = GetFaction().name;
            name = $"AU|{aiName}|{GenesData.shortName}|{factionName}|{internalIDStr}";
        }

        public override string ToString()
        {
            var factionName = GetFaction().name;
            return $"Utility|{GenesData.name}|{factionName}|";
        }

        public void ChangeBrainTo(AIBrainMachine newBrain)
        {
            brain = newBrain;
        }

        public AIBrainMachine GetBrain() => brain;

        public AIGenesSO GetGenesData() => GenesData;
    }
}