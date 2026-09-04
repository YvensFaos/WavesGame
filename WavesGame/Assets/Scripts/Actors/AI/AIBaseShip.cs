/*
 * Copyright (c) 2026 Yvens R Serpa [https://github.com/YvensFaos/]
 *
 * This work is licensed under the Creative Commons Attribution 4.0 International License.
 * To view a copy of this license, visit http://creativecommons.org/licenses/by/4.0/
 * or see the LICENSE file in the root directory of this repository.
 */

using System.Collections;
using Core;
using Core.Simulation;
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
        
        protected abstract IEnumerator TurnAI();

        public override string ToString()
        {
            return $"{base.ToString()}; faction={GetFaction()}; kills={kills}";
        }

        public string ToLlmString()
        {
            return $"[{name}]; faction={GetFaction()}; currentHealth={GetCurrentHealth()}.";
        }
    }
}