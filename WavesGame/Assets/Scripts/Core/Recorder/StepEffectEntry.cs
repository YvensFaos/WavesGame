/*
 * Copyright (c) 2026 Yvens R Serpa [https://github.com/YvensFaos/]
 *
 * This work is licensed under the Creative Commons Attribution 4.0 International License.
 * To view a copy of this license, visit http://creativecommons.org/licenses/by/4.0/
 * or see the LICENSE file in the root directory of this repository.
 */

using System;
using UnityEngine;
using UUtils;

namespace Core.Recorder
{
    [Serializable]
    public class StepEffectEntryJson : WavesEntryJson
    {
        [SerializeField] private string stepId;
        [SerializeField] private string targetId;
        [SerializeField] private int damage;

        public StepEffectEntryJson(string eventType, int turn, long timeStamp, string stepId, string targetId,
            int damage) : base(eventType, turn, timeStamp)
        {
            this.stepId = stepId;
            this.targetId = targetId;
            this.damage = damage;
        }
    }

    public class StepEffectEntry : WavesEntry
    {
        private string _stepId;
        private string _targetId;
        private int _damage;

        public StepEffectEntry(string stepId, string targetId, int damage) : base(WavesRecordEntryType.StepFX)
        {
            _stepId = stepId;
            _targetId = targetId;
            _damage = damage;
        }

        public override void PerformEntry()
        {
            DebugUtils.DebugLogMsg($"Step FX. Origin: {_stepId}. Target: {_targetId}. Damage: {_damage}.",
                DebugUtils.DebugType.Temporary);
        }
        
        protected override string ToJson()
        {
            return JsonUtility.ToJson(new StepEffectEntryJson(
                WavesRecordEntryTypeExtensions.WavesRecordEntryTypeToString(WavesRecordEntryType.StepFX), turn,
                timeStamp, _stepId, _targetId, _damage));
        }
    }
}