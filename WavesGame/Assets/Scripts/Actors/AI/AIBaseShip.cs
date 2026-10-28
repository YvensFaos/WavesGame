/*
 * Copyright (c) 2026 Yvens R Serpa [https://github.com/YvensFaos/]
 *
 * This work is licensed under the Creative Commons Attribution 4.0 International License.
 * To view a copy of this license, visit http://creativecommons.org/licenses/by/4.0/
 * or see the LICENSE file in the root directory of this repository.
 */

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Core;
using Core.Simulation;
using Grid;
using Lean.Pool;
using NaughtyAttributes;
using UnityEngine;
using UUtils;

namespace Actors.AI
{
    public abstract class AIBaseShip : NavalShip
    {
        [Header("Score")] [SerializeField, ReadOnly]
        protected int kills;

        protected string AIName;
        
        [SerializeField]
        protected AIDisplayTile displayTilePrefab;

        private List<AIDisplayTile> _displayTiles;

        protected override void Awake()
        {
            base.Awake();
            AIName = AINamer.GetRandomFullName();
            kills = 0;
            _displayTiles = new List<AIDisplayTile>();
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
            DelayHelper.DelayOneFrame(this, () =>
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
            });
        }
        
        protected abstract IEnumerator TurnAI();

        protected override List<GridUnit> GetMovementPath(GridUnit unit)
        {
            return GridManager.GetSingleton()
                .GetManhattanPathFromToAStarWithCondition(GetUnit().Index(), unit.Index(), StepsAvailable,
                    (currentTile, initialUnit, finalUnit) =>
                    {
                        //First check if it is blocked
                        if (currentTile.Type() == GridUnitType.Blocked)
                        {
                            return false;
                        }
                        if (currentTile.Equals(initialUnit) || currentTile.Equals(finalUnit))
                        {
                            return true;
                        } 
                        return !currentTile.HasActorOfType<WaveActor>();
                    });
        }

        public string GetAIName()
        {
            return AIName;
        }

        public override string ToString()
        {
            return $"{base.ToString()}; faction={GetFaction()}; kills={kills}";
        }

        public string ToLlmString()
        {
            return $"[{name}]; faction={GetFaction()}; currentHealth={GetCurrentHealth()}.";
        }

        protected void DisplayMoveTiles(AIBaseShip aiBaseShip, int availableSteps)
        {
            var walkableUnits = GridManager.GetSingleton().GetGridUnitsInRadiusManhattan(aiBaseShip.GetUnit().Index(), availableSteps);
            DisplayTiles(walkableUnits);
        }
        
        private void DisplayTiles(List<GridUnit> units)
        {
            HideTiles();
            foreach (var displayTile in units.Select(unit => LeanPool.Spawn(displayTilePrefab, unit.transform)))
            {
                _displayTiles.Add(displayTile);
            }
        }

        protected void DisplayAttackTiles(AIBaseShip aiBaseShip)
        {
            var position = aiBaseShip.GetUnit().Index();
            var cannonData = aiBaseShip.NavalCannon.GetCannonSo;
            var attackableFromUnit = GridManager.GetSingleton().GetGridUnitsForMoveType(cannonData.targetAreaType,
                position, cannonData.area, cannonData.deadZone);
            DisplayTiles(attackableFromUnit);
        }

        protected void HideTiles()
        {
            if (_displayTiles is not { Count: > 0 }) return;
            for (var i = _displayTiles.Count - 1; i >= 0; i--)
            {
                if (_displayTiles[i] != null)
                {
                    LeanPool.Despawn(_displayTiles[i].gameObject);    
                }
            }
            _displayTiles.Clear();
            _displayTiles = new List<AIDisplayTile>();
        }
    }
}