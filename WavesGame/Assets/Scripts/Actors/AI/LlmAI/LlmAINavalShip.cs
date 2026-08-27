/*
 * Copyright (c) 2026 Yvens R Serpa [https://github.com/YvensFaos/]
 *
 * This work is licensed under the Creative Commons Attribution 4.0 International License.
 * To view a copy of this license, visit http://creativecommons.org/licenses/by/4.0/
 * or see the LICENSE file in the root directory of this repository.
 */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using Core.Recorder;
using FALLA;
using FALLA.Helper;
using Grid;
using Newtonsoft.Json;
using UnityEngine;
using UUtils;

namespace Actors.AI.LlmAI
{
    [Serializable]
    internal class LlmAction
    {
        public string reasoning = "";
        public int[] movement = { -1, -1 };
        public int[] attack = { -1, -1 };
        [JsonProperty("move_after_attack")] public int[] moveAfterAttack = { -1, -1 };

        public static Vector2Int GetAsVector2Int(int[] pair)
        {
            return pair == null ? new Vector2Int(-1, -1) : new Vector2Int(pair[0], pair[1]);
        }
    }

    public class LlmAINavalShip : AIBaseShip
    {
        [Header("LLM")] [SerializeField] private LlmCallerObject llmCaller;
        [SerializeField] private float requestTimeOutTimer = 1.0f;
        [SerializeField] private LlmPromptSo basePrompt;
        [SerializeField] private List<Faction> enemyFactions;

        protected override void Start()
        {
            base.Start();
            AssessUtils.CheckRequirement(ref llmCaller, this);
            UpdateName();
        }

        public override void UpdateName()
        {
            var internalIDStr = internalID.ToString();
            if (llmCaller == null || llmCaller.GetLlmType() == LlmType.Custom)
            {
                name = $"LLMAgent|Utility|{internalIDStr}";
            }
            else
            {
                var llmName = $"{llmCaller.GetLlmType().ToString()}|{llmCaller.GetLlmModel()}";
                var factionName = GetFaction().name;
                name = $"LLMAgent|{llmName}|{factionName}|{internalIDStr}";
            }
        }

        private static bool IsValidLlmAction(Vector2Int action)
        {
            return action.x != -1 && action.y != -1;
        }

        protected override IEnumerator TurnAI()
        {
            //Wait two frames for the logger to get ready
            yield return null;

            var maxAttempts = 5;
            var breakTime = 5.0f;

            yield return new WaitForSeconds(0.05f);

            DebugUtils.DebugLogMsg($"Request Timer. Wait for {requestTimeOutTimer} seconds.",
                DebugUtils.DebugType.System);
            yield return new WaitForSeconds(requestTimeOutTimer);
            DebugUtils.DebugLogMsg($"Request Timer Finished.", DebugUtils.DebugType.System);

            var prompt = LlmAiPromptGenerator.GeneratePrompt(this, basePrompt, enemyFactions);
            DebugUtils.DebugLogMsg(prompt, DebugUtils.DebugType.Temporary);

            var retry = true;
            var faultyMessage = false;
            var result = "";
            do
            {
                var stopwatch = Stopwatch.StartNew();
                DebugUtils.DebugLogMsg("Prompt sent...", DebugUtils.DebugType.Temporary);
                try
                {
                    llmCaller.CallLlm(prompt);
                }
                catch (Exception e)
                {
                    DebugUtils.DebugLogMsg($"Exception: {e.Message}.",
                        DebugUtils.DebugType.Error);
                    StopTimer(stopwatch);
                    RecordInvalidResponse(InvalidResponseType.Exception, e.Message);
                    faultyMessage = true;
                }

                if (faultyMessage)
                {
                    DebugUtils.DebugLogMsg($"Faulty message! Retrying in {breakTime} seconds...",
                        DebugUtils.DebugType.Error);
                    yield return new WaitForSeconds(breakTime);
                    breakTime *= 1.25f;
                    continue;
                }

                yield return new WaitUntil(() => llmCaller.IsReady());

                var llmGenericResponse = llmCaller.GetResponse();
                if (!llmGenericResponse.Success || string.IsNullOrEmpty(llmGenericResponse.Response))
                {
                    var msg =
                        $"No response exception: {llmGenericResponse.Response} Success:{llmGenericResponse.Success}.";
                    DebugUtils.DebugLogMsg(
                        msg,
                        DebugUtils.DebugType.Error);

                    RecordInvalidResponse(InvalidResponseType.NoResponse, msg);
                    StopTimer(stopwatch);
                    DebugUtils.DebugLogMsg($"Retrying in {breakTime} seconds...", DebugUtils.DebugType.Error);
                    yield return new WaitForSeconds(breakTime);
                    breakTime *= 1.25f;
                    continue;
                }

                result = llmGenericResponse.Response;
                StopTimer(stopwatch);
                retry = false;

                DebugUtils.DebugLogMsg($"Result received: [{result}].", DebugUtils.DebugType.Temporary);
            } while (retry && --maxAttempts >= 0);

            DebugUtils.DebugLogMsg(result, DebugUtils.DebugType.Verbose);

            var jsonResult = Sanitizer.ExtractJson(result);
            DebugUtils.DebugLogMsg(jsonResult, DebugUtils.DebugType.System);

            var actions = new LlmAction();
            try
            {
                actions = JsonConvert.DeserializeObject<LlmAction>(jsonResult);
            }
            catch (Exception e)
            {
                DebugUtils.DebugLogMsg($"Exception {e.Message}.", DebugUtils.DebugType.Error);
                RecordInvalidResponse(InvalidResponseType.InvalidOutput,
                    $"Output was: [{jsonResult}]. Error: {e.Message}.");
                DebugUtils.DebugLogErrorMsg(e.Message);
            }

            DebugUtils.DebugLogMsg(actions.reasoning, DebugUtils.DebugType.System);

            var shouldMove = false;
            var shouldAttack = false;
            var shouldMoveAfterAttack = false;
            var movement = new Vector2Int(-1, -1);
            var attack = new Vector2Int(-1, -1);
            var moveAfterAttack = new Vector2Int(-1, -1);

            try
            {
                movement = LlmAction.GetAsVector2Int(actions.movement);
                attack = LlmAction.GetAsVector2Int(actions.attack);
                moveAfterAttack = LlmAction.GetAsVector2Int(actions.moveAfterAttack);
                RecordCommands(movement, attack, moveAfterAttack);

                shouldMove = IsValidLlmAction(movement);
                shouldAttack = IsValidLlmAction(attack);
                shouldMoveAfterAttack = IsValidLlmAction(moveAfterAttack);
            }
            catch (Exception e)
            {
                DebugUtils.DebugLogMsg($"Exception {e.Message}.", DebugUtils.DebugType.Error);
                DebugUtils.DebugLogErrorMsg(e.Message);
                RecordInvalidResponse(InvalidResponseType.InvalidOutput,
                    $"Output was: [{jsonResult}]. Error: {e.Message}.");
            }

            if (shouldMove)
            {
                yield return StartCoroutine(LlmMoveCoroutine(movement, actions.reasoning));
            }

            if (shouldAttack)
            {
                yield return StartCoroutine(LlmAttackCoroutine(attack, actions.reasoning));
            }

            if (shouldMoveAfterAttack)
            {
                yield return StartCoroutine(LlmMoveCoroutine(moveAfterAttack, actions.reasoning, true));
            }

            FinishAITurn();
            yield break;

            void StopTimer(Stopwatch stopwatch)
            {
                stopwatch.Stop();
                var elapsed = stopwatch.ElapsedMilliseconds;
                var timeText = $"Request response in {elapsed} ms.";
                DebugUtils.DebugLogMsg(timeText,
                    DebugUtils.DebugType.System);
            }
        }

        private IEnumerator LlmMoveCoroutine(Vector2Int moveToPosition, string reasoning, bool moveAfterAttack = false)
        {
            var canMove = GridManager.GetSingleton().CheckGridPosition(moveToPosition, out var moveGridUnit);
            var finishedMoving = false;
            if (canMove)
            {
                var moved = MoveTo(moveGridUnit, _ => { finishedMoving = true; }, true);
                if (!moved)
                {
                    RecordFailedToMove(moveGridUnit.Index(), reasoning, moveAfterAttack);
                }
            }
            else
            {
                DebugUtils.DebugLogMsg($"Could not move to {moveToPosition}.", DebugUtils.DebugType.Error);
                finishedMoving = true;
                RecordFailedToMove(moveToPosition, reasoning, moveAfterAttack);
            }

            yield return new WaitUntil(() => finishedMoving);
        }

        private IEnumerator LlmAttackCoroutine(Vector2Int attackPosition, string reasoning)
        {
            while (TryToAct())
            {
                var hasValidTarget = GridManager.GetSingleton().CheckGridPosition(attackPosition, out var targetUnit);
                if (!hasValidTarget && targetUnit.ActorsCount() <= 0)
                {
                    RecordInvalidTargetChosen(targetUnit, reasoning);
                    continue;
                }

                var canAttack = GridManager.GetSingleton()
                    .CanAttackFrom(currentUnit.Index(), attackPosition, navalCannon.GetCannonSo);
                if (canAttack)
                {
                    DebugUtils.DebugLogMsg($"{name} attacks {targetUnit}!", DebugUtils.DebugType.System);
                    // TODO LevelController.GetSingleton().AddAttackLog(targetUnit.Index(), this, name);
                    var damage = CalculateDamage();
                    RecordAttack(targetUnit.GetActor(), targetUnit, damage, reasoning);
                    kills = targetUnit.DamageActors(damage);
                    yield return new WaitForSeconds(1.5f);
                }
                else
                {
                    var cannotReachMsg = $"Cannot reach target at {targetUnit}.";
                    RecordCannotReachAttack(targetUnit.GetActor(), targetUnit, reasoning);
                    DebugUtils.DebugLogMsg(cannotReachMsg, DebugUtils.DebugType.Error);
                }
            }

            yield return null;
        }

        private void RecordCommands(Vector2Int movement, Vector2Int attack, Vector2Int moveAfterAttack)
        {
            if (!WavesRecorder.TryToGetSingleton(out var recorder)) return;
            var command = new CommandRecordEntry(name, GetFaction(), movement, attack, moveAfterAttack);
            recorder.RecordNewEntry(command);
        }

        private void RecordInvalidResponse(InvalidResponseType type, string message)
        {
            if (!WavesRecorder.TryToGetSingleton(out var recorder)) return;
            var invalidResponse = new InvalidResponseEntry(name, GetFaction(), type, message);
            recorder.RecordNewEntry(invalidResponse);
        }

        private void RecordCannotReachAttack(GridActor targetActor, GridUnit targetUnit, string reasoning)
        {
            if (!WavesRecorder.TryToGetSingleton(out var recorder)) return;
            var invalidCannotReachEntry = new InvalidAttemptRecordEntry(name, GetFaction(),
                InvalidAttemptType.OutOfReach, currentUnit.Index(),
                targetActor, targetUnit.Index(), reasoning);
            if (targetActor is WaveActor)
            {
                invalidCannotReachEntry.AppendComment($"Attacked a wave");
            }

            recorder.RecordNewEntry(invalidCannotReachEntry);
        }

        private void RecordFailedToMove(Vector2Int targetUnit, string reasoning, bool moveAfterAttack)
        {
            if (!WavesRecorder.TryToGetSingleton(out var recorder)) return;
            var invalidCannotReachEntry = new InvalidAttemptRecordEntry(name, GetFaction(),
                InvalidAttemptType.FailedToMove, currentUnit.Index(),
                null, targetUnit, reasoning);
            if (moveAfterAttack)
            {
                invalidCannotReachEntry.AppendComment($"Movement after attacking.");
            }

            recorder.RecordNewEntry(invalidCannotReachEntry);
        }

        public string GetLlmInfo()
        {
            return llmCaller != null && llmCaller.GetLlmType() != LlmType.Custom
                ? $"{llmCaller.GetLlmType().ToString()}-{llmCaller.GetLlmModel()}-{basePrompt.name}"
                : "Utility";
        }

        public void SetCaller(LlmCallerObject caller)
        {
            llmCaller = caller;
        }

        public void ChangeBasePrompt(LlmPromptSo promptSo)
        {
            basePrompt = promptSo;
        }

        public void AddEnemyFaction(Faction faction)
        {
            enemyFactions.Add(faction);
        }

        public LlmPromptSo GetPrompt() => basePrompt;

        public LlmCallerObject GetCaller() => llmCaller;
    }
}