/*
 * Copyright (c) 2026 Yvens R Serpa [https://github.com/YvensFaos/]
 *
 * This work is licensed under the MIT License.
 * To view a copy of this license, visit https://opensource.org/licenses/MIT
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

        public void LogGameStart(string map, int randomSeed, int maxTurns, List<NavalActor> ships, string extraText,
            List<WaveActor> waveActors, List<ObstacleActor> obstacleActors, string recordingIdentifier)
        {
            StartRecording(recordingIdentifier);
            RecordNewEntry(new WavesGameInfoEntry(map, randomSeed, maxTurns, GameManager.GetSingleton().GetSessionUid(),
                extraText, ships, waveActors,
                obstacleActors));
        }

        private void StartRecording(string recordingIdentifier)
        {
            recorder.StartRecording(recordingIdentifier, ".txt");
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