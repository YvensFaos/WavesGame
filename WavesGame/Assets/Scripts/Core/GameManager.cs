/*
 * Copyright (c) 2025 Yvens R Serpa [https://github.com/YvensFaos/]
 *
 * This work is licensed under the Creative Commons Attribution 4.0 International License.
 * To view a copy of this license, visit http://creativecommons.org/licenses/by/4.0/
 * or see the LICENSE file in the root directory of this repository.
 */

using System;
using UnityEngine;
using UUtils;

namespace Core
{
    public class GameManager : StrongSingleton<GameManager>
    {
        [SerializeField] private GameManagerSettings gameManagerSettings;
        [SerializeField] private string gameSessionUid;

        protected override void Awake()
        {
            base.Awake();
            if (MarkedToDie) return;
            DebugUtils.enabledDebugTypes = gameManagerSettings.enabledDebugTypes;
            DebugUtils.enabledDebugLogTypes = gameManagerSettings.enabledDebugLogTypes;
            
            gameSessionUid = Guid.NewGuid().ToString("N")[..10];
        }

        private void Start()
        {
            if (MarkedToDie) return;
            if (!gameManagerSettings.logToFile) return;
            var logName = $"{gameManagerSettings.logName}-{TimestampHelper.GetSimplifiedTimestamp()}";
            DebugUtils.LogToFile(logName);
        }

        private void OnApplicationQuit()
        {
            if (!gameManagerSettings.logToFile) return;
            if (MarkedToDie) return;
            DebugUtils.CloseLog();
        }

        private void OnValidate()
        {
            DebugUtils.enabledDebugTypes = gameManagerSettings.enabledDebugTypes;
        }

        public GameManagerSettings GetSettings() => gameManagerSettings;
        public string GetSessionUid() => gameSessionUid;
    }
}