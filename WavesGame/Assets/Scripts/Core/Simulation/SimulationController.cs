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
using Actors;
using Grid;
using TMPro;
using UnityEngine;
using UUtils;

namespace Core.Simulation
{
    public class SimulationController : GameController
    {
        [SerializeField] private TextMeshProUGUI simulationText;

        private Dictionary<Faction, List<NavalShip>> _navalShips;

        public void Initialize(Dictionary<Faction, List<NavalShip>> navalShipsDictionary)
        {
            _navalShips = navalShipsDictionary;
        }

        public Coroutine StartSimulation(int seed)
        {
            randomSeed = seed;
            controllerCoroutine = StartCoroutine(SimulationCoroutine());
            return controllerCoroutine;
        }

        private IEnumerator SimulationCoroutine()
        {
            running = true;
            yield return new WaitForEndOfFrame();
            //Wait for one frame for all elements to be initialized
            yield return null;

            Random.InitState(randomSeed);
            levelActors = FindObjectsByType<GridActor>(FindObjectsInactive.Include, FindObjectsSortMode.InstanceID)
                .ToList();
            //Initialize level goal elements
            levelGoal.Initialize(levelActors);
            yield return null;

            simulationText.text = "Simulation";
            turnText.text = "Turns";

            if (TurnManager.TryToGetSingleton(out var turnManager))
            {
                turnManager.Initialize();
            }

            var allNavalShips = new List<NavalShip>();
            levelNavalActors = new List<NavalActor>();
            levelActionableActors = new List<LevelActorPair>();
            foreach (var pair in _navalShips)
            {
                allNavalShips.AddRange(pair.Value);
                levelNavalActors.AddRange(pair.Value);
            }

            allNavalShips.Sort();

            levelActionableActors.AddRange(allNavalShips.Select(actor => new LevelActorPair(actor)));
            allNavalShips.ForEach(navalShip =>
            {
                DebugUtils.DebugLogMsg($"Creating actor UI {navalShip.name}", DebugUtils.DebugType.System);
                AddLevelActorToTurnBar(navalShip);
            });

            var firstActor = allNavalShips[0];
            CursorController.GetSingleton().MoveToIndex(firstActor.GetUnit().Index());
            yield return 0.5f;

            if (TurnManager.TryToGetSingleton(out turnManager))
            {
                turnText.text = $"Turn = {turnManager.GetTurnNumber()}";
            }

            //Start level
            var enumerator = levelActionableActors.GetEnumerator();
            var continueLevel = true;
            var victory = false;
            var gameOver = false;
            while (continueLevel)
            {
                //There are no actors left. Finish the level cycle.
                if (actorTurnUIs.Count == 0)
                {
                    continueLevel = false;
                    continue;
                }

                while (enumerator.MoveNext())
                {
                    // If the current is valid, then proceed with its turn.
                    if (!enumerator.Current) continue;
                    currentActor = enumerator.Current?.One;
                    endTurn = false;
                    if (currentActor is NavalShip navalShip)
                    {
                        var turnUI = GetActorTurnUI(navalShip);
                        turnUI.ToggleAvailability(true);

                        CursorController.GetSingleton().MoveToIndex(navalShip.GetUnit().Index());
                        yield return 0.5f;

                        navalShip.StartTurn();
                        // Move the cursor to the ship
                        yield return new WaitUntil(() => endTurn);

                        // Check if the naval ship was not destroyed during its own turn.
                        if (navalShip == null) continue;
                        navalShip.EndTurn();

                        if (enumerator.Current is { Two: true })
                        {
                            turnUI.ToggleAvailability(false);
                        }
                    }
                    else
                    {
                        yield return new WaitUntil(() => endTurn);
                    }
                }

                enumerator.Dispose();
                //Finished going through all characters
                levelGoal.SurvivedTurn();

                if (TurnManager.TryToGetSingleton(out turnManager))
                {
                    turnManager.NextTurn();
                    turnText.text = $"Turn = {turnManager.GetTurnNumber()}";
                }

                victory = levelGoal.CheckGoal();
                gameOver = levelGoal.CheckGameOver();
                if (victory || gameOver)
                {
                    continueLevel = false;
                }
                else
                {
                    //If there are no more enumerators ahead, then start from the beginning.
                    enumerator = levelActionableActors.GetEnumerator();
                }
            }

            enumerator.Dispose();
            running = false;
        }

        public override void NotifyDestroyedActor(NavalShip navalShip)
        {
            //Does not finish the level if the level controller is not controlling the game.
            if (!running) return;
            if (currentActor.Equals(navalShip))
            {
                //End current turn is for the actor being destroyed
                EndTurnForCurrentActor();
            }

            //Set the pair as false, so its level should be skipped.
            var actionPair = levelActionableActors.Find(pair => pair.One.Equals(navalShip));
            actionPair.Two = false;

            //Remove the naval ship from the list of active naval ships.
            levelNavalActors.Remove(navalShip);

            DebugUtils.DebugLogMsg($"Naval Ship: {navalShip.name} destroyed. Checking for level finish...",
                DebugUtils.DebugType.System);
            if (levelGoal.CheckGoalActor(navalShip))
            {
                //Game level goal was achieved
                FinishLevel(true);
            }

            if (levelGoal.CheckGameOver())
            {
                FinishLevel(false);
            }

            var actorTurnUI = actorTurnUIs.Find(turnUI => turnUI.NavalShip.Equals(navalShip));
            if (actorTurnUI == null) return;
            if (actorTurnUIs == null) return;
            actorTurnUIs.Remove(actorTurnUI);
            if (actorTurnUI.gameObject == null) return;
            Destroy(actorTurnUI.gameObject);
        }

        protected override void FinishLevel(bool win)
        {
            throw new System.NotImplementedException();
        }
    }
}