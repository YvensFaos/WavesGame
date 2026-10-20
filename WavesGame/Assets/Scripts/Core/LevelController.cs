/*
 * Copyright (c) 2026 Yvens R Serpa [https://github.com/YvensFaos/]
 *
 * This work is licensed under the Creative Commons Attribution 4.0 International License.
 * To view a copy of this license, visit http://creativecommons.org/licenses/by/4.0/
 * or see the LICENSE file in the root directory of this repository.
 */

using System.Collections;
using System.Linq;
using Actors;
using Core.Recorder;
using Grid;
using NaughtyAttributes;
using TMPro;
using UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UUtils;

namespace Core
{
    public class LevelController : GameController
    {
        [SerializeField] private bool initiativeBased = true;
        [SerializeField] private bool logLevel;

        [SerializeField, Scene] private string nextLevelName;

        [Header("Controllers")] [SerializeField]
        private bool recordLevel;
        [SerializeField] private string extra = "";
        [SerializeField] private EndLevelPanelUI endLevelPanelUI;
        [SerializeField] private TextMeshProUGUI levelGoalText;
        [SerializeField] private TextMeshProUGUI uidText;

        private bool _finishedLevel;
        private WavesRecorder _recorder;

        protected override void Awake()
        {
            base.Awake();
            AssessUtils.CheckRequirement(ref levelGoal, this);
        }

        private void Start()
        {
            endLevelPanelUI.gameObject.SetActive(false);
            controllerCoroutine = StartCoroutine(LevelCoroutine());
        }

        private IEnumerator LevelCoroutine()
        {
            //Wait for one frame for all elements to be initialized
            yield return null;
            running = true;

            Random.InitState(randomSeed);
            // var recorderInfo = "";
            levelActors = levelActors.FindAll(actor => actor != null);

            yield return null;
            levelNavalActors = levelNavalActors.FindAll(levelNavalActor => levelNavalActor != null);

            //Initialize level goal elements
            levelGoal.Initialize(levelActors);
            yield return null;

            if (TurnManager.TryToGetSingleton(out var turnManager))
            {
                turnManager.Initialize();
            }

            yield return null;

            levelActionableActors = levelActionableActors.FindAll(levelActorPair => levelActorPair?.One != null);

            levelGoalText.text = levelGoal.GetLevelMessage();
            turnText.text = "Turns";

            //Roll initiatives and order turns
            if (initiativeBased)
            {
                levelActionableActors.ForEach(actorPair => actorPair.One.RollInitiative());
            }

            levelActionableActors.Sort((pairOne, pairTwo) =>
                pairTwo.One.Initiative.CompareTo(pairOne.One.Initiative));

            levelActionableActors.ForEach(actorPair =>
            {
                DebugUtils.DebugLogMsg(
                    $"Creating actor UI {actorPair.One.gameObject.name} [{actorPair.One.Initiative}]",
                    DebugUtils.DebugType.System);
                AddLevelActorToTurnBar(actorPair.One);
            });

            var cursorController = CursorController.GetSingleton();
            var firstActor = levelActionableActors[0].One;
            cursorController.MoveToIndex(firstActor.GetUnit().Index());

            if (TurnManager.TryToGetSingleton(out turnManager))
            {
                turnText.text = $"Turn = {turnManager.GetTurnNumber()}";
            }

            var uid = GameManager.GetSingleton().GetSessionUid();
            uidText.text = uid;

            if (recordLevel && WavesRecorder.TryToGetSingleton(out _recorder))
            {
                //This is used when the level is being recorded for regular gaming procedures, rather than by the scheduler
                var recorderInfo = GetLevelRecordingName();
                DebugUtils.DebugLogMsg("Recorder found. Recording level.", DebugUtils.DebugType.System);
                
                var navalActors =
                    FindObjectsByType<NavalActor>(FindObjectsInactive.Exclude).ToList();
                var waveActors =
                    FindObjectsByType<WaveActor>(FindObjectsInactive.Exclude)
                        .ToList();
                var obstacleActors = FindObjectsByType<ObstacleActor>(FindObjectsInactive.Exclude).ToList();
                
                _recorder.LogGameStart(SceneManager.GetActiveScene().name, 
                    randomSeed, 
                    levelGoal.GetMaxTurns(), 
                    navalActors,
                    $"{extra}[uid]",
                    waveActors,
                    obstacleActors,
                    recorderInfo);
                _recorder.RecordNewEntry(new GoalRecordEntry(levelGoal));
            }

            //Start level
            var enumerator = levelActionableActors.GetEnumerator();
            var continueLevel = true;
            var victory = false;
            var gameOver = false;
            
            var gridManager = GridManager.GetSingleton();
            RecordScenario(gridManager);
            
            while (continueLevel)
            {
                //TODO log level
                
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
                        
                        if (WavesInputTracer.TryToGetSingleton(out var inputTracer))
                        {
                            if (inputTracer is WavesInputTracer wavesInputTracer)
                            {
                                wavesInputTracer.SetCurrentNavalShip(navalShip);    
                            }
                        }
                        
                        // Move the cursor to the ship
                        if (cursorController.MovingAnimation())
                        {
                            DebugUtils.DebugLogMsg($"Cursor is still moving...", DebugUtils.DebugType.System);
                            yield return new WaitUntil(() => !cursorController.MovingAnimation());
                        }

                        // Move the cursor to the ship
                        cursorController.MoveToIndex(navalShip.GetUnit().Index());
                        yield return 0.75f;
                        
                        navalShip.StartTurn();

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
                
                RecordScenario(gridManager);
                var currentActors = levelActors.Where(actor => actor != null && actor is NavalActor)
                    .Select(actor => (NavalActor)actor).ToList();
                RecordGameState(currentActors);

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

            FinishLevel(victory);
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

        /// <summary>
        /// Used to force a finish level screen for Player Level Recording.
        /// </summary>
        public void ForceFinishLevel()
        {
            _finishedLevel = true;
            StopCoroutine(controllerCoroutine);
            CursorController.GetSingleton().FinishLevel();
            endLevelPanelUI.gameObject.SetActive(true);
            endLevelPanelUI.OpenEndLevelPanel(nextLevelName, true);
        }

        protected override void FinishLevel(bool win)
        {
            //Does not finish the level if the level controller is not controlling the game.
            if (!running) return;
            //Prevents finishing the level more than once
            if (_finishedLevel) return;
            _finishedLevel = true;
            StopCoroutine(controllerCoroutine);

            DebugUtils.DebugLogMsg($"Level ended: {(win ? "Victory!" : "Defeat!")}", DebugUtils.DebugType.System);
            CursorController.GetSingleton().FinishLevel();
            if (_recorder != null)
            {
                _recorder.RecordNewEntry(new EndGameRecordEntry(levelGoal.GetLevelMessage(), levelGoal.GetWinnerFaction(), win));
                DebugUtils.DebugLogMsg("Recording complete.", DebugUtils.DebugType.System);
                _recorder.Stop();
            }

            // Delays one frame to finish writing all the necessary information on the logs and recorders.
            DelayHelper.DelayOneFrame(this, () =>
            {
                endLevelPanelUI.gameObject.SetActive(true);
                endLevelPanelUI.OpenEndLevelPanel(nextLevelName, win);
            });
        }
    }
}