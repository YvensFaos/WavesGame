/*
 * Copyright (c) 2026 Yvens R Serpa [https://github.com/YvensFaos/]
 *
 * This work is licensed under the Creative Commons Attribution 4.0 International License.
 * To view a copy of this license, visit http://creativecommons.org/licenses/by/4.0/
 * or see the LICENSE file in the root directory of this repository.
 */

using System.Collections;
using Core;
using Core.Recorder;
using Core.Simulation;
using Grid;
using NaughtyAttributes;
using UnityEngine;
using UUtils;

namespace Actors.AI
{
    public abstract class AIBaseShip : NavalShip
    {
        [Header("Score")] [SerializeField, ReadOnly]
        protected int kills;

        protected override void Awake()
        {
            base.Awake();
            kills = 0;
        }

        public override void StartTurn()
        {
            base.StartTurn();
            CursorController.GetSingleton().ToggleActive(false);
            StartCoroutine(TurnAI());
        }

        public override void EndTurn()
        {
            base.EndTurn();
            CursorController.GetSingleton().ToggleActive(true);
        }

        protected virtual void FinishAITurn()
        {
            if (LevelController.TryToGetSingleton(out var levelController))
            {
                levelController.EndTurnForCurrentActor();
            }

            if (SimulationController.TryToGetSingleton(out var simulationController))
            {
                simulationController.EndTurnForCurrentActor();
            }
            DebugUtils.DebugLogMsg($"{name} has finished its turn.", DebugUtils.DebugType.System);
        }
        
        protected void RecordAttack(GridActor targetActor, GridUnit unit, int damage, string reasoning)
        {
            if (!WavesRecorder.TryToGetSingleton(out var recorder)) return;
            if (targetActor == null)
            {
                RecordInvalidTargetChosen(unit, reasoning);
                return;
            }

            if (targetActor is NavalShip navalShip && navalShip.GetFaction().Equals(GetFaction()))
            {
                var invalidAttempt = new InvalidAttemptRecordEntry(name, GetFaction(), InvalidAttemptType.FriendlyFire,
                    currentUnit.Index(),
                    targetActor, unit.Index(), reasoning);
                recorder.RecordNewEntry(invalidAttempt);
            }

            var attackRecordEntry = new AttackRecordEntry(name, GetFaction(), targetActor.GetUnit().Index(),
                targetActor.name, damage);
            if (targetActor is WaveActor)
            {
                attackRecordEntry.AppendComment($"Attacked a wave");
            }

            recorder.RecordNewEntry(attackRecordEntry);
        }

        protected void RecordInvalidTargetChosen(GridUnit targetUnit, string reasoning)
        {
            if (!WavesRecorder.TryToGetSingleton(out var recorder)) return;
            var invalidCannotReachEntry = new InvalidAttemptRecordEntry(name, GetFaction(),
                InvalidAttemptType.InvalidTarget, currentUnit.Index(),
                null, targetUnit.Index(), reasoning);
            if (targetUnit.ActorsCount() == 0)
            {
                invalidCannotReachEntry.AppendComment("No valid targets at the attacked position.");
            }

            recorder.RecordNewEntry(invalidCannotReachEntry);
        }

        protected abstract IEnumerator TurnAI();

        public int GetKills() => kills;

        public override string ToString()
        {
            return $"{base.ToString()}; faction={GetFaction()}; kills={GetKills()}";
        }

        public string ToLlmString()
        {
            return $"[{name}]; faction={GetFaction()}; currentHealth={GetCurrentHealth()}.";
        }
    }
}