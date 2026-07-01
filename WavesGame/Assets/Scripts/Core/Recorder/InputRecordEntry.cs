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
        [SerializeField] public string actionName;
        [SerializeField] public string actionMap;
        [SerializeField] public string actionPhase;
        [SerializeField] public string actionValue;
        [SerializeField] public string actionDevice;
        [SerializeField] public double actionStartTime;

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
        private readonly string _actionName;
        private readonly string _actionMap;
        private readonly string _actionPhase;
        private readonly string _actionValue;
        private readonly string _actionDevice;
        private readonly double _actionStartTime;

        public InputRecordEntry(InputActionTrace.ActionEventPtr actionEventPtr, int turn, long timeStamp) : base(
            WavesRecordEntryType.Input, turn, timeStamp)
        {
            var action = actionEventPtr.action;
            _actionName = action.name;
            _actionMap = action.actionMap.name;
            _actionPhase = action.phase.ToString();
            _actionStartTime = actionEventPtr.startTime;

            var readValueAsObject = action.ReadValueAsObject();
            _actionValue = action.phase switch
            {
                InputActionPhase.Disabled => readValueAsObject != null ? readValueAsObject.ToString() : "",
                InputActionPhase.Waiting => "",
                InputActionPhase.Started => readValueAsObject != null ? readValueAsObject.ToString() : "",
                InputActionPhase.Performed => readValueAsObject != null ? readValueAsObject.ToString() : "",
                InputActionPhase.Canceled => "",
                _ => throw new ArgumentOutOfRangeException()
            };
            _actionDevice = "";
            if (action.activeControl is { device: not null })
            {
                _actionDevice = action.activeControl.device.name;
            }
        }

        public override void PerformEntry()
        {
            DebugUtils.DebugLogMsg(
                $"Input: {_actionName}, {_actionPhase}, {_actionValue}, {_actionDevice}, {_actionStartTime}.",
                DebugUtils.DebugType.Verbose);
        }

        protected override string ToJson()
        {
            return JsonConvert.SerializeObject(new InputRecordEntryJson(_actionName, _actionMap, _actionPhase,
                _actionValue, _actionDevice, _actionStartTime,
                WavesRecordEntryTypeExtensions.WavesRecordEntryTypeToString(eventType),
                turn, timeStamp));
        }
    }
}