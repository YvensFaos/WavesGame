/*
 * Copyright (c) 2026 Yvens R Serpa [https://github.com/YvensFaos/]
 *
 * This work is licensed under the Creative Commons Attribution 4.0 International License.
 * To view a copy of this license, visit http://creativecommons.org/licenses/by/4.0/
 * or see the LICENSE file in the root directory of this repository.
 */

using System;
using System.Collections.Generic;
using Actors;
using Actors.AI;
using Grid;
using NaughtyAttributes;
using TMPro;
using UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UUtils;

namespace Core
{
    public abstract class GameController : WeakSingleton<GameController>
    {
        [Header("References")] [SerializeField]
        protected RectTransform actorTurnsHolder;

        [SerializeField] protected ActorTurnUI actorTurnUIPrefab;
        [SerializeField, ReadOnly] protected List<ActorTurnUI> actorTurnUIs;
        [SerializeField, ReadOnly] protected List<LevelActorPair> levelActionableActors;
        [SerializeField, ReadOnly] protected List<NavalActor> levelNavalActors;
        [SerializeField] protected TextMeshProUGUI turnText;
        [Header("Data")] [SerializeField] protected int randomSeed = 6;
        [SerializeField] protected List<GridActor> levelActors;

        [Header("Level Specific")] [SerializeField]
        protected LevelGoal levelGoal;

        protected bool running;
        protected NavalActor currentActor;
        protected bool endTurn;
        protected Coroutine controllerCoroutine;

        /// <summary>
        /// Allows the LevelController to continue.
        /// </summary>
        public void EndTurnForCurrentActor()
        {
            endTurn = true;
        }

        protected ActorTurnUI GetActorTurnUI(NavalShip navalShip)
        {
            return actorTurnUIs.Find(actorTurnUI => actorTurnUI.NavalShip.Equals(navalShip));
        }

        public bool IsCurrentActor(NavalActor navalActor)
        {
            return currentActor.Equals(navalActor);
        }

        public int AddLevelActor(GridActor actor)
        {
            levelActors.Add(actor);
            if (actor is not NavalActor navalActor) return levelActors.Count;
            levelNavalActors.Add(navalActor);
            switch (navalActor.NavalType)
            {
                case NavalActorType.Player:
                case NavalActorType.Enemy:
                    if (navalActor is NavalShip navalShip)
                    {
                        levelActionableActors ??= new List<LevelActorPair>();
                        levelActionableActors.Add(new LevelActorPair(navalShip));
                        return levelActionableActors.Count;
                    }

                    break;
                case NavalActorType.Collectable:
                case NavalActorType.Obstacle:
                case NavalActorType.Wave:
                    return levelNavalActors.Count;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            return levelActors.Count;
        }

        protected void AddLevelActorToTurnBar(NavalShip navalShip)
        {
            var newActorTurnUI = Instantiate(actorTurnUIPrefab, actorTurnsHolder);
            newActorTurnUI.Initialize(navalShip);
            actorTurnUIs.Add(newActorTurnUI);
        }

        public void NotifyDestroyedActor(NavalActor navalActor)
        {
            //Does not finish the level if the level controller is not controlling the game.
            if (!running) return;
            DebugUtils.DebugLogMsg($"Naval Actor {navalActor.name} notified Game Controller of its destruction.",
                DebugUtils.DebugType.Verbose);
        }

        public abstract void NotifyDestroyedActor(NavalShip navalShip);

        public void NotifyDestroyedActor(NavalTarget navalTarget)
        {
            //Does not finish the level if the level controller is not controlling the game.
            if (!running) return;
            DebugUtils.DebugLogMsg($"Target: {navalTarget.name} destroyed. Checking for level finish...",
                DebugUtils.DebugType.System);
            if (levelGoal.CheckGoalActor(navalTarget))
            {
                //Game level goal was achieved
                FinishLevel(true);
            }
        }

        protected abstract void FinishLevel(bool win);

        public void StopLevel()
        {
            StopCoroutine(controllerCoroutine);
            running = false;
        }

        public void RemoveFactionShip(AIBaseShip aiBaseShip)
        {
            levelGoal.RemoveFactionCount(aiBaseShip);
        }

        public string GetLevelRecordingName()
        {
            return
                $"{SceneManager.GetActiveScene().name}-{TimestampHelper.GetSimplifiedTimestamp()}-{levelGoal.GetLevelMessage()}";
        }
        
        public string GetLevelMessageAndTime()
        {
            return
                $"{TimestampHelper.GetSimplifiedTimestamp()}-{levelGoal.GetLevelMessage()}";
        }

        public static void TryToUseGameController(Action<GameController> function)
        {
            if (TryToGetSingleton(out var gameController))
            {
                function(gameController);
            }
            else
            {
                DebugUtils.DebugLogWarningMsg("No valid Game Controller found!");
            }
        }

        public LevelGoal GetLevelGoal() => levelGoal;
        public int GetRandomSeed() => randomSeed;
        public List<NavalActor>.Enumerator GetNavalActorsEnumerator() => levelNavalActors.GetEnumerator();
    }
}