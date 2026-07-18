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
using NaughtyAttributes;
using TMPro;
using UI;
using UnityEngine;
using UUtils;

namespace Core.Simulation
{
    public class SimulationController : WeakSingleton<SimulationController>
    {
        [Header("References")] [SerializeField]
        private RectTransform actorTurnsHolder;

        [SerializeField] private ActorTurnUI actorTurnUIPrefab;
        [SerializeField] private TextMeshProUGUI simulationText;
        [SerializeField] private TextMeshProUGUI turnText;
        [SerializeField, ReadOnly] private List<ActorTurnUI> actorTurnUIs;

        [Header("Data")] [SerializeField] private List<GridActor> levelActors;

        [Header("Level Specific")] [SerializeField]
        private LevelGoal levelGoal;

        private Dictionary<Faction, List<NavalShip>> _navalShips;
        private NavalActor _currentActor;

        private bool _victory;
        private bool _endTurn;

        public void Initialize(Dictionary<Faction, List<NavalShip>> navalShipsDictionary)
        {
            _navalShips = navalShipsDictionary;
        }

        public Coroutine StartSimulation(int randomSeed)
        {
            return StartCoroutine(SimulationCoroutine(randomSeed));
        }

        private IEnumerator SimulationCoroutine(int randomSeed)
        {
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

            var allNavalShips = new List<NavalShip>();
            var levelActionableActors = new List<LevelActorPair>();
            foreach (var pair in _navalShips)
            {
                allNavalShips.AddRange(pair.Value);
                levelActionableActors.AddRange(pair.Value.Select(actor => new LevelActorPair(actor)));
            }

            allNavalShips.ForEach(navalShip =>
            {
                DebugUtils.DebugLogMsg($"Creating actor UI {navalShip.name}", DebugUtils.DebugType.System);
                AddLevelActorToTurnBar(navalShip);
            });

            var firstActor = allNavalShips[0];
            CursorController.GetSingleton().MoveToIndex(firstActor.GetUnit().Index());
            yield return 0.5f;
            turnText.text = $"Turn = {levelGoal.GetCurrentTurn()}";

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
                    _currentActor = enumerator.Current?.One;
                    _endTurn = false;
                    if (_currentActor is NavalShip navalShip)
                    {
                        var turnUI = GetActorTurnUI(navalShip);
                        turnUI.ToggleAvailability(true);

                        CursorController.GetSingleton().MoveToIndex(navalShip.GetUnit().Index());
                        yield return 0.5f;

                        navalShip.StartTurn();
                        // Move the cursor to the ship
                        yield return new WaitUntil(() => _endTurn);

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
                        yield return new WaitUntil(() => _endTurn);
                    }
                }

                enumerator.Dispose();
                //Finished going through all characters
                levelGoal.SurvivedTurn();
                levelGoal.NextTurn();
                turnText.text = $"Turn = {levelGoal.GetCurrentTurn()}";

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
            if (gameOver)
            {
                victory = false;
            }

            _victory = victory;
        }
        
        /// <summary>
        /// Allows the LevelController to continue.
        /// </summary>
        public void EndTurnForCurrentActor()
        {
            _endTurn = true;
        }

        private ActorTurnUI GetActorTurnUI(NavalShip navalShip)
        {
            return actorTurnUIs.Find(actorTurnUI => actorTurnUI.NavalShip.Equals(navalShip));
        }

        private void AddLevelActorToTurnBar(NavalShip navalShip)
        {
            var newActorTurnUI = Instantiate(actorTurnUIPrefab, actorTurnsHolder);
            newActorTurnUI.Initialize(navalShip);
            actorTurnUIs.Add(newActorTurnUI);
        }

        public bool Victory => _victory;
    }
}