/*
 * Copyright (c) 2026 Yvens R Serpa [https://github.com/YvensFaos/]
 *
 * This work is licensed under the Creative Commons Attribution 4.0 International License.
 * To view a copy of this license, visit http://creativecommons.org/licenses/by/4.0/
 * or see the LICENSE file in the root directory of this repository.
 */

using System;
using System.Collections;
using Actors.AI.Brain;
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

            AIAction act;
            do
            {
                act = brain.CalculateAction(this, actionsLeft, remainingSteps, out var target);
                var targetString = target != null ? target.ToString() : "[No Target]";
                var utilityReasoning = $"{name} has selected action {act} with target {targetString}.";
                DebugUtils.DebugLogMsg($"{utilityReasoning}", DebugUtils.DebugType.System);
                switch (act)
                {
                    case AIAction.None:
                        DebugUtils.DebugLogMsg($"{name} has no action to do!", DebugUtils.DebugType.System);
                        act = AIAction.EndTurn;
                        goto case AIAction.EndTurn;
                    case AIAction.Movement:
                        if (target == null)
                        {
                            DebugUtils.DebugLogErrorMsg($"Invalid target movement position [null].");
                            break;
                        }
                        _calculatingAction = true;
                        //TODO
                        var move = MoveTo(target.GetUnit(), unit =>
                        {
                            _calculatingAction = false;
                        }, true);
                        yield return new WaitUntil(() => !_calculatingAction);
                        remainingSteps = move ? RemainingSteps : 0;
                        break;
                    case AIAction.Attack:
                        if (target == null)
                        {
                            DebugUtils.DebugLogErrorMsg($"Invalid target movement position [null].");
                            break;
                        }
                        var targetUnit = target.GetUnit();
                        if (targetUnit.ActorsCount() <= 0)
                        {
                            DebugUtils.DebugLogErrorMsg($"Target position has no valid targets [{targetUnit}].");
                            break;
                        }
                        var firstActor = targetUnit.GetActor();
                        DebugUtils.DebugLogMsg($"{name} attacks {firstActor}!", DebugUtils.DebugType.System);
                        if (TryToAct())
                        {
                            var damage = CalculateDamage();
                            RecordAttack(firstActor, targetUnit, damage, utilityReasoning);
                            kills += targetUnit.DamageActors(damage);
                            yield return new WaitForSeconds(0.7f);
                        }
                        else
                        {
                            DebugUtils.DebugLogErrorMsg($"{name} cannot act! No more valid actions this turn.");
                        }
                        actionsLeft = ActionsLeft;
                        break;
                    case AIAction.EndTurn:
                        DebugUtils.DebugLogMsg($"{name} finishes its turn!", DebugUtils.DebugType.System);
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
            name = $"AIAgent-Utility|{GenesData.name}|{factionName}|{internalIDStr}";
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

        public AIGenesSO GetGenesData() => GenesData;
    }
}