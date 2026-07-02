using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Core;
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
        public int[] moveAfterAttack = { -1, -1 };

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

        private int _internalWrongMovementCount;
        private int _internalWrongAttackCount;
        private int _internalTotalRequestCount;
        private int _internalMovementAttemptCount;
        private int _internalAttackAttemptCount;
        private int _internalFaultyMessageCount;
        private List<long> _internalTimers;
        private List<int> _internalAttempts;

        protected override void Awake()
        {
            base.Awake();
            AssessUtils.CheckRequirement(ref llmCaller, this);
        }

        protected override void Start()
        {
            base.Start();
            _internalTimers = new List<long>();
            _internalAttempts = new List<int>();
            UpdateName();
        }

        public void UpdateName()
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

            var attempt = 0;
            var maxAttempts = 5;
            var breakTime = 5.0f;
            LevelController.GetSingleton().AddInfoLog($"Start turn;{maxAttempts},{breakTime}", name);
            yield return new WaitForSeconds(0.05f);

            DebugUtils.DebugLogMsg($"Request Timer. Wait for {requestTimeOutTimer} seconds.",
                DebugUtils.DebugType.System);
            yield return new WaitForSeconds(requestTimeOutTimer);
            DebugUtils.DebugLogMsg($"Request Timer Finished.", DebugUtils.DebugType.System);

            var prompt = LlmAiPromptGenerator.GeneratePrompt(this, basePrompt, enemyFactions);
            PromptInfo($"{basePrompt.name};{prompt.Length}");
            DebugUtils.DebugLogMsg(prompt, DebugUtils.DebugType.Temporary);

            var retry = true;
            var faultyMessage = false;
            var result = "";
            do
            {
                attempt++;
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
                    _internalFaultyMessageCount++;
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

                _internalTotalRequestCount++;
                yield return new WaitUntil(() => llmCaller.IsReady());

                var llmGenericResponse = llmCaller.GetResponse();
                if (!llmGenericResponse.Success || string.IsNullOrEmpty(llmGenericResponse.Response))
                {
                    var msg =
                        $"No response exception: {llmGenericResponse.Response} Success:{llmGenericResponse.Success}.";
                    DebugUtils.DebugLogMsg(
                        msg,
                        DebugUtils.DebugType.Error);
                    LevelController.GetSingleton()
                        .AddInfoLog(
                            msg,
                            name);
                    RecordInvalidResponse(InvalidResponseType.NoResponse, msg);
                    StopTimer(stopwatch);
                    _internalFaultyMessageCount++;
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

            LevelController.GetSingleton().AddDataLog($"\"attempts\":{attempt}", name);
            _internalAttempts.Add(attempt);

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
                LevelController.GetSingleton().AddInfoLog($"Casting exception! {e.Message}", name);
                LevelController.GetSingleton().AddInfoLog($"Output was: [{jsonResult}]", name);
                RecordInvalidResponse(InvalidResponseType.InvalidOutput, $"Output was: [{jsonResult}]. Error: {e.Message}.");
                DebugUtils.DebugLogErrorMsg(e.Message);
                _internalFaultyMessageCount++;
            }

            DebugUtils.DebugLogMsg(actions.reasoning, DebugUtils.DebugType.System);

            if (!string.IsNullOrEmpty(actions.reasoning) && WavesRecorder.TryToGetSingleton(out var wavesRecorder))
            {
                wavesRecorder.RecordNewEntry(new ReasoningRecordEntry(name, LevelController.GetSingleton().GetTurn(),
                    LevelController.GetSingleton().GetTimeStamp(), actions.reasoning));
            }

            LevelController.GetSingleton().AddReasonLog(actions.reasoning, name);

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
                shouldMove = IsValidLlmAction(movement);
                shouldAttack = IsValidLlmAction(attack);
                shouldMoveAfterAttack = IsValidLlmAction(moveAfterAttack);
            }
            catch (Exception e)
            {
                DebugUtils.DebugLogMsg($"Exception {e.Message}.", DebugUtils.DebugType.Error);
                LevelController.GetSingleton().AddInfoLog($"Casting exception on trying to act! {e.Message}", name);
                DebugUtils.DebugLogErrorMsg(e.Message);
                RecordInvalidResponse(InvalidResponseType.InvalidOutput, $"Output was: [{jsonResult}]. Error: {e.Message}.");
            }

            LevelController.GetSingleton()
                .AddInfoLog($"Flags;{shouldMove};{shouldAttack};{shouldMoveAfterAttack}", name);

            if (shouldMove)
            {
                _internalMovementAttemptCount++;
                yield return StartCoroutine(LlmMoveCoroutine(movement, actions.reasoning));
            }

            if (shouldAttack)
            {
                _internalAttackAttemptCount++;
                yield return StartCoroutine(LlmAttackCoroutine(attack, actions.reasoning));
            }

            if (shouldMoveAfterAttack)
            {
                _internalMovementAttemptCount++;
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
                LevelController.GetSingleton().AddTimeInfoToLog($"\"request\":{elapsed}", name);
                _internalTimers.Add(elapsed);
            }
        }

        private IEnumerator LlmMoveCoroutine(Vector2Int moveToPosition, string reasoning, bool moveAfterAttack = false)
        {
            var canMove = GridManager.GetSingleton().CheckGridPosition(moveToPosition, out var moveGridUnit);
            var finishedMoving = false;
            if (canMove)
            {
                LevelController.GetSingleton().AddMovementLog(moveGridUnit.Index(), name);
                var moved = MoveTo(moveGridUnit, _ => { finishedMoving = true; }, true);
                if (!moved)
                {
                    RecordFailedToMove(moveGridUnit.Index(), reasoning, moveAfterAttack);
                    _internalWrongMovementCount++;
                }
            }
            else
            {
                DebugUtils.DebugLogMsg($"Could not move to {moveToPosition}.", DebugUtils.DebugType.Error);
                LevelController.GetSingleton().AddInfoLog($"Failed to move to {moveToPosition}", name);
                finishedMoving = true;
                RecordFailedToMove(moveToPosition, reasoning, moveAfterAttack);
                _internalWrongMovementCount++;
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
                    LevelController.GetSingleton().AddInfoLog($"No valid target chosen", name);
                    RecordInvalidTargetChosen(targetUnit, reasoning);
                    _internalWrongAttackCount++;
                    continue;
                }

                var canAttack = GridManager.GetSingleton()
                    .CanAttackFrom(currentUnit.Index(), attackPosition, navalCannon.GetCannonSo);
                if (canAttack)
                {
                    DebugUtils.DebugLogMsg($"{name} attacks {targetUnit}!", DebugUtils.DebugType.System);
                    LevelController.GetSingleton().AddAttackLog(targetUnit.Index(), this, name);
                    var damage = CalculateDamage();
                    RecordAttack(targetUnit.GetActor(), targetUnit, damage, reasoning);
                    kills = targetUnit.DamageActors(damage);
                    LevelController.GetSingleton()
                        .AddInfoLog($"Attacked succeeded at {targetUnit}. Kill count = {kills}.", name);
                    yield return new WaitForSeconds(1.5f);
                }
                else
                {
                    var cannotReachMsg = $"Cannot reach target at {targetUnit}.";
                    RecordCannotReachAttack(targetUnit.GetActor(), targetUnit, reasoning);
                    DebugUtils.DebugLogMsg(cannotReachMsg, DebugUtils.DebugType.Error);
                    LevelController.GetSingleton().AddInfoLog(cannotReachMsg, name);
                    _internalWrongAttackCount++;
                }
            }

            yield return null;
        }

        private void RecordInvalidTargetChosen(GridUnit targetUnit, string reasoning)
        {
            if (!WavesRecorder.TryToGetSingleton(out var recorder)) return;
            var invalidCannotReachEntry = new InvalidAttemptRecordEntry(name, LevelController.GetSingleton().GetTurn(),
                LevelController.GetSingleton().GetTimeStamp(), InvalidAttemptType.InvalidTarget, currentUnit.Index(),
                null, targetUnit.Index(), reasoning);
            if (targetUnit.ActorsCount() == 0)
            {
                invalidCannotReachEntry.AppendComment("No valid targets at the attacked position.");
            }

            recorder.RecordNewEntry(invalidCannotReachEntry);
        }

        private void RecordInvalidResponse(InvalidResponseType type, string message)
        {
            if (!WavesRecorder.TryToGetSingleton(out var recorder)) return;
            var invalidResponse = new InvalidResponseEntry(name, LevelController.GetSingleton().GetTurn(),
                LevelController.GetSingleton().GetTimeStamp(), type, message);
            recorder.RecordNewEntry(invalidResponse);
        }

        private void RecordCannotReachAttack(GridActor targetActor, GridUnit targetUnit, string reasoning)
        {
            if (!WavesRecorder.TryToGetSingleton(out var recorder)) return;
            var invalidCannotReachEntry = new InvalidAttemptRecordEntry(name, LevelController.GetSingleton().GetTurn(),
                LevelController.GetSingleton().GetTimeStamp(), InvalidAttemptType.OutOfReach, currentUnit.Index(),
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
            var invalidCannotReachEntry = new InvalidAttemptRecordEntry(name, LevelController.GetSingleton().GetTurn(),
                LevelController.GetSingleton().GetTimeStamp(), InvalidAttemptType.FailedToMove, currentUnit.Index(),
                null, targetUnit, reasoning);
            if (moveAfterAttack)
            {
                invalidCannotReachEntry.AppendComment($"Movement after attacking.");
            }

            recorder.RecordNewEntry(invalidCannotReachEntry);
        }

        private void RecordAttack(GridActor targetActor, GridUnit unit, int damage, string reasoning)
        {
            if (!WavesRecorder.TryToGetSingleton(out var recorder)) return;
            if (targetActor == null)
            {
                RecordInvalidTargetChosen(unit, reasoning);
                return;
            }

            if (targetActor is AIBaseShip aiBaseShip && aiBaseShip.GetFaction().Equals(GetFaction()))
            {
                var invalidAttempt = new InvalidAttemptRecordEntry(name, LevelController.GetSingleton().GetTurn(),
                    LevelController.GetSingleton().GetTimeStamp(), InvalidAttemptType.FriendlyFire, currentUnit.Index(),
                    aiBaseShip, unit.Index(), reasoning);
                recorder.RecordNewEntry(invalidAttempt);
            }

            var attackRecordEntry = new AttackRecordEntry(name, targetActor.GetUnit().Index(), targetActor.name, damage,
                LevelController.GetSingleton().GetTurn(), LevelController.GetSingleton().GetTimeStamp());
            if (targetActor is WaveActor)
            {
                attackRecordEntry.AppendComment($"Attacked a wave");
            }

            recorder.RecordNewEntry(attackRecordEntry);
        }

        private void PromptInfo(string promptInfo)
        {
            LevelController.GetSingleton().AddPromptLog(promptInfo, name);
        }

        protected override void FinishAITurn()
        {
            LevelController.GetSingleton().AddInfoLog("Finish turn", name);
            base.FinishAITurn();
        }

        protected override void DestroyActor()
        {
            LevelController.GetSingleton().AddInfoLog("Destroyed", name);
            LogFinalInformation();
            base.DestroyActor();
        }

        public void LogFinalInformation()
        {
            var averageRequest = (float)_internalTimers.Sum(timer => timer) / _internalTimers.Count;
            var maxRequest = _internalTimers is { Count: > 0 } ? _internalTimers.Max(timer => timer) : -1;
            var minRequest = _internalTimers is { Count: > 0 } ? _internalTimers.Min(timer => timer) : -1;
            var averageAttempts = (float)_internalAttempts.Sum(attempt => attempt) / _internalAttempts.Count;
            LevelController.GetSingleton().AddDataLog($"\"internalWrongMovementCount\":{_internalWrongMovementCount}" +
                                                      $",\"internalWrongAttackCount\":{_internalWrongAttackCount}" +
                                                      $",\"internalTotalRequestCount\":{_internalTotalRequestCount}" +
                                                      $",\"internalMovementAttemptCount\":{_internalMovementAttemptCount}" +
                                                      $",\"internalAttackAttemptCount\":{_internalAttackAttemptCount}" +
                                                      $",\"internalFaultyMessageCount\":{_internalFaultyMessageCount}" +
                                                      $",\"averageRequestTime\":{averageRequest},\"averageRequestTimeCount\":{_internalTimers.Count}" +
                                                      $",\"maxRequestTime\":{maxRequest},\"minRequest\":{minRequest}" +
                                                      $",\"averageAttempts\":{averageAttempts}" +
                                                      $",\"kills\":{kills}", name);
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

        public LlmPromptSo GetPrompt() => basePrompt;

        public LlmCallerObject GetCaller() => llmCaller;
    }
}