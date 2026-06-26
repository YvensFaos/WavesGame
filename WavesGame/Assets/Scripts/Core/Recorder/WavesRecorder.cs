/*
 * Copyright (c) 2026 Yvens R Serpa [https://github.com/YvensFaos/]
 *
 * This work is licensed under the Creative Commons Attribution 4.0 International License.
 * To view a copy of this license, visit http://creativecommons.org/licenses/by/4.0/
 * or see the LICENSE file in the root directory of this repository.
 */

using System.Collections.Generic;
using Actors;
using UnityEngine;
using UUtils;
using UUtils.GameRecorder;

namespace Core.Recorder
{
    [RequireComponent(typeof(GameRecorder))]
    public class WavesRecorder : WeakSingleton<WavesRecorder>
    {
        [SerializeField] private GameRecorder recorder;

        public void LogGameStart(string map, int randomSeed, List<NavalActor> ships, string recordingIdentifier)
        {
            StartRecording(recordingIdentifier);
            RecordNewEntry(new WavesGameInfoEntry(map, randomSeed, ships));
        }

        private void StartRecording(string recordingIdentifier)
        {
            recorder.StartRecording(recordingIdentifier);
        }

        public void RecordNewEntry(RecordEntry entry)
        {
            recorder.RecordNewEntry(entry);
        }

        public void Stop()
        {
            recorder.StopRecording();
        }
    }
}