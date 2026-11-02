/*
 * Copyright (c) 2026 Yvens R Serpa [https://github.com/YvensFaos/]
 *
 * This work is licensed under the Creative Commons Attribution 4.0 International License.
 * To view a copy of this license, visit http://creativecommons.org/licenses/by/4.0/
 * or see the LICENSE file in the root directory of this repository.
 */

using System;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;
using UUtils;

namespace Core.Recorder
{
    [Serializable]
    public class InputRecordEntryJson : WavesEntryJson
    {
        [SerializeField] [JsonProperty(DefaultValueHandling = DefaultValueHandling.Include)] public string actionName;
        [SerializeField] [JsonProperty(DefaultValueHandling = DefaultValueHandling.Include)] public string actionMap;
        [SerializeField] [JsonProperty(DefaultValueHandling = DefaultValueHandling.Include)] public string actionPhase;
        [SerializeField] [JsonProperty(DefaultValueHandling = DefaultValueHandling.Include)] public string actionValue;
        [SerializeField] [JsonProperty(DefaultValueHandling = DefaultValueHandling.Include)] public string actionDevice;
        [SerializeField] [JsonProperty(DefaultValueHandling = DefaultValueHandling.Include)] public double actionStartTime;

        public InputRecordEntryJson(string actionName, string actionMap, string actionPhase, string actionValue,
            string actionDevice,
            double actionStartTime,
            string eventType, int turn, long timeStamp) : base(eventType, turn, timeStamp)
        {
            this.actionName = actionName;
            this.actionMap = actionMap;
            this.actionPhase = actionPhase;
            this.actionValue = actionValue;
            this.actionDevice = actionDevice;
            this.actionStartTime = actionStartTime;
        }
    }

    public class InputRecordEntry : WavesEntry
    {
        protected readonly string ActionName;
        protected readonly string ActionMap;
        protected readonly string ActionPhase;
        protected readonly string ActionValue;
        protected readonly string ActionDevice;
        protected readonly double ActionStartTime;

        public InputRecordEntry(InputActionTrace.ActionEventPtr actionEventPtr) : base(
            WavesRecordEntryType.Input)
        {
            var action = actionEventPtr.action;
            ActionName = action.name;
            ActionMap = action.actionMap.name;
            ActionPhase = action.phase.ToString();
            ActionStartTime = actionEventPtr.startTime;

            var readValueAsObject = action.ReadValueAsObject();
            ActionValue = action.phase switch
            {
                InputActionPhase.Disabled => readValueAsObject != null ? readValueAsObject.ToString() : "",
                InputActionPhase.Waiting => "",
                InputActionPhase.Started => readValueAsObject != null ? readValueAsObject.ToString() : "",
                InputActionPhase.Performed => readValueAsObject != null ? readValueAsObject.ToString() : "",
                InputActionPhase.Canceled => "",
                _ => throw new ArgumentOutOfRangeException()
            };
            ActionDevice = "";
            if (action.activeControl is { device: not null })
            {
                ActionDevice = action.activeControl.device.name;
            }
        }

        public override void PerformEntry()
        {
            DebugUtils.DebugLogMsg(
                $"Input: {ActionName}, {ActionPhase}, {ActionValue}, {ActionDevice}, {ActionStartTime}.",
                DebugUtils.DebugType.Verbose);
        }

        protected override string ToJson()
        {
            return JsonConvert.SerializeObject(new InputRecordEntryJson(ActionName, ActionMap, ActionPhase,
                ActionValue, ActionDevice, ActionStartTime,
                WavesRecordEntryTypeExtensions.WavesRecordEntryTypeToString(eventType),
                turn, timeStamp), GetJsonSerializerSettings());
        }
    }
}