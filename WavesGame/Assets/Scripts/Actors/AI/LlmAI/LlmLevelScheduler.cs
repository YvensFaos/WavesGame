/*
 * Copyright (c) 2026 Yvens R Serpa [https://github.com/YvensFaos/]
 *
 * This work is licensed under the Creative Commons Attribution 4.0 International License.
 * To view a copy of this license, visit http://creativecommons.org/licenses/by/4.0/
 * or see the LICENSE file in the root directory of this repository.
 */

using System.Collections.Generic;
using System.Linq;
using Core;
using Core.Recorder;
using FALLA;
using Grid;
using NaughtyAttributes;
using UnityEngine;
using UnityEngine.SceneManagement;
using UUtils;

namespace Actors.AI.LlmAI
{
    public class LlmLevelScheduler : StrongSingleton<LlmLevelScheduler>
    {
        [SerializeField, ReadOnly] private List<LlmCallerObject> callers;
        [SerializeField] private List<LlmScheduleSo> schedules;
        [SerializeField, ReadOnly] private LlmScheduleSo currentSchedule;
        [SerializeField, ReadOnly] private int internalCounter;
        [SerializeField, ReadOnly] private int internalRepetition;

        public int InternalRepetition => internalRepetition;

        protected override void Awake()
        {
            base.Awake();
            if (MarkedToDie) return;
            schedules.ForEach(schedule => schedule.Initialize());
        }

        public void BeginNewLevel()
        {
            callers = FindObjectsByType<LlmCallerObject>(FindObjectsSortMode.None).ToList();
        }

        public bool CheckValidLevel()
        {
            if (internalCounter < schedules.Count) return true;
            DebugUtils.DebugLogMsg($"Finish! All schedules done, current -> {internalCounter}.",
                DebugUtils.DebugType.System);
            return false;
        }
        
        public List<NavalActor> SetupLevel(List<GridActor> levelActors)
        {
            currentSchedule = schedules[internalCounter];
            internalRepetition = currentSchedule.InternalRepetitionsCount;

            var llmActors = levelActors.Select(actor =>
            {
                if (actor is LlmAINavalShip llm)
                {
                    return llm;
                }
                return null;
            }).ToList().FindAll(actor => actor != null);

            var aiFactions = currentSchedule.GetFactions();
            var ships = new List<NavalActor>();
            
            foreach (var aiFaction in aiFactions)
            {
                var factionLlmActors = llmActors.FindAll(actor => actor.GetFaction().Equals(aiFaction));
                var pair = currentSchedule.GetFactionPair(aiFaction);
                pair.SetCaller(callers);

                foreach (var llmAINavalShip in factionLlmActors)
                {
                    llmAINavalShip.SetCaller(pair.Caller);
                    var prompt = pair.GetPromptSo();
                    if (prompt != null)
                    {
                        llmAINavalShip.ChangeBasePrompt(prompt);
                    }

                    llmAINavalShip.UpdateName();
                    ships.Add(llmAINavalShip);
                }
            }

            var customFactions = currentSchedule.factionPairs.FindAll(pair => pair.Two.modelPair.One == LlmType.Custom);
            var levelController = LevelController.GetSingleton();
            foreach (var customFaction in customFactions)
            {
                var faction = customFaction.One;
                var llmFactionShips = llmActors.FindAll(actor => actor.GetFaction().Equals(faction));

                foreach (var llmFactionShip in llmFactionShips)
                {
                    var llmFactionTransform = llmFactionShip.transform;
                    var newAiBaseShip = Instantiate(customFaction.AIBaseShipPrefab, llmFactionTransform.position,
                        llmFactionTransform.rotation);
                    if (newAiBaseShip is AINavalShip aiBaseShip)
                    {
                        aiBaseShip.GenesData = customFaction.AIGenes;
                    }

                    //TODO check if this works for the grid
                    var unit = llmFactionShip.GetUnit();
                    unit.RemoveActor(llmFactionShip);
                    unit.AddActor(newAiBaseShip);
                    newAiBaseShip.SetInitiative(llmFactionShip.OverrideInitiative);
                    levelController.RemoveFactionShip(llmFactionShip);
                    Destroy(llmFactionShip.gameObject);
                    ships.Add(newAiBaseShip);
                }
            }
            ships = ships.FindAll(ship => ship != null && ship.enabled);

            if (!WavesRecorder.TryToGetSingleton(out var wavesRecorder)) return ships;
            DebugUtils.DebugLogMsg("Recorder found. Recording level.", DebugUtils.DebugType.System);
            var recorderFileName = $"{currentSchedule}-{internalRepetition}-{TimestampHelper.GetSimplifiedTimestamp()}";
            wavesRecorder.LogGameStart(SceneManager.GetActiveScene().name, levelController.GetRandomSeed(), ships, recorderFileName);
            wavesRecorder.RecordNewEntry(new GoalRecordEntry(levelController.GetLevelGoal()));
            return ships;
        }

        public void FinishLevel(LevelGoal levelGoal)
        {
            DebugUtils.DebugLogMsg($"Finished level, current -> {internalCounter}.", DebugUtils.DebugType.System);

            currentSchedule = schedules[internalCounter];
            if (currentSchedule.Use())
            {
                internalCounter++;
            }

            var winnerFaction = levelGoal.GetWinnerFaction();
            if (winnerFaction != null)
            {
                DebugUtils.DebugLogMsg($"Finished level, winner -> {winnerFaction}.", DebugUtils.DebugType.System);
                var winnerPair = currentSchedule.GetFactionPair(winnerFaction);
                LevelController.GetSingleton()
                    .AddInfoLog(
                        winnerPair.Two.modelPair.One == LlmType.Custom
                            ? $"Utility {winnerPair.One.name} won."
                            : $"LLM {winnerPair.Caller} won.", "LevelGoal");
            }
            else
            {
                DebugUtils.DebugLogMsg($"Finished level, DRAW!", DebugUtils.DebugType.System);
                LevelController.GetSingleton().AddInfoLog($"DRAW!", "LevelGoal");
            }

            if (WavesRecorder.TryToGetSingleton(out var wavesRecorder))
            {
                wavesRecorder.Stop();
            }

            DelayHelper.Delay(this, 5.0f, SceneLoader.ResetCurrentScene);
        }

        [Button("Shuffle Schedules")]
        public void ShuffleSchedule()
        {
            RandomHelper<LlmScheduleSo>.ShuffleList(ref schedules);
        }

        public string GetCurrentScheduleInfo()
        {
            if (internalCounter >= schedules.Count) return "no-schedule";
            var schedule = schedules[internalCounter];
            var repetition = schedule.InternalRepetitionsCount;

            return $"{schedule}-{repetition}-{TimestampHelper.GetSimplifiedTimestamp()}";
        }
    }
}