/*
 * Copyright (c) 2026 Yvens R Serpa [https://github.com/YvensFaos/]
 *
 * This work is licensed under the Creative Commons Attribution 4.0 International License.
 * To view a copy of this license, visit http://creativecommons.org/licenses/by/4.0/
 * or see the LICENSE file in the root directory of this repository.
 */

using Core.Recorder;
using NaughtyAttributes;
using UnityEngine;
using UUtils;

namespace Core
{
    public class TurnManager : WeakSingleton<TurnManager>
    {
        [SerializeField, ReadOnly] private int turnNumber;

        public void Initialize()
        {
            DebugUtils.DebugLogMsg($"Turn Manager initialized at object {name}.", DebugUtils.DebugType.System);
            turnNumber = 0;
        }
        
        public static long GetTimeStamp()
        {
            return (long)Time.timeSinceLevelLoad;
        }

        public void NextTurn()
        {
            turnNumber++;
            RecordTurn();
        }

        private void RecordTurn()
        {
            if (!WavesRecorder.TryToGetSingleton(out var recorder)) return;
            recorder.RecordNewEntry(new TurnRecordEntry(turnNumber));
        }
        
        public int GetTurnNumber() => turnNumber;
    }
}