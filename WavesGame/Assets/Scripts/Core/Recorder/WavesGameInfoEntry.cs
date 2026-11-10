/*
 * Copyright (c) 2026 Yvens R Serpa [https://github.com/YvensFaos/]
 *
 * This work is licensed under the MIT License.
 * To view a copy of this license, visit https://opensource.org/licenses/MIT
 * or see the LICENSE file in the root directory of this repository.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using Actors;
using Newtonsoft.Json;
using UnityEngine;
using UUtils;

namespace Core.Recorder
{
    [Serializable]
    public class WavesGameInfoRecordEntryJson : WavesEntryJson
    {
        [SerializeField] public string map;
        [SerializeField] public int randomSeed;
        [SerializeField] public int maxTurns;
        [SerializeField] public string startTime;
        [SerializeField] public string sessionId;
        [SerializeField] public string extraText;
        [SerializeField] public string deviceMode;
        [SerializeField] public string processorType;
        [SerializeField] public string graphicsDeviceName;
        [SerializeField] public List<NavalActorEntryJson> navalActorEntryJsons;
        [SerializeField] public List<WaveActorEntryJson> waveActorEntryJsons;
        [SerializeField] public List<ObstacleEntryJson> obstacleEntryJsons;

        public WavesGameInfoRecordEntryJson(string eventType, int turn, long timeStamp, string startTime, string sessionId,
            string extraText, string map, string deviceMode, string processorType, string graphicsDeviceName,
            int randomSeed, int maxTurns,
            List<NavalActor> navalActors, List<WaveActor> waveActors, List<ObstacleActor> obstacleActors) : base(
            eventType, turn, timeStamp)
        {
            this.map = map;
            this.randomSeed = randomSeed;
            this.maxTurns = maxTurns;
            this.startTime = startTime;
            this.sessionId = sessionId;
            this.extraText = extraText;
            this.deviceMode = deviceMode;
            this.processorType = processorType;
            this.graphicsDeviceName = graphicsDeviceName;
            navalActorEntryJsons = navalActors.Select(ship => new NavalActorEntryJson(ship)).ToList();
            waveActorEntryJsons = waveActors.Select(waveActor => new WaveActorEntryJson(waveActor)).ToList();
            obstacleEntryJsons = obstacleActors.Select(obstacleActor => new ObstacleEntryJson(obstacleActor)).ToList();

            DebugUtils.DebugLogMsg(
                $"Device Mode {this.deviceMode} - Processor Type {this.processorType} - Graphics Device Name {this.graphicsDeviceName}",
                DebugUtils.DebugType.Temporary);
        }
    }

    public class WavesGameInfoEntry : WavesEntry
    {
        private readonly string _map;
        private readonly int _randomSeed;
        private readonly int _maxTurns;
        private readonly string _startTime;
        private readonly string _sessionId;
        private readonly string _extraText;
        private readonly string _deviceMode;
        private readonly string _processorType;
        private readonly string _graphicsDeviceName;
        private readonly List<NavalActor> _navalActors;
        private readonly List<WaveActor> _waveActors;
        private readonly List<ObstacleActor> _obstacleActors;

        public WavesGameInfoEntry(string map, int randomSeed, int maxTurns, string sessionId, string extraText,
            List<NavalActor> navalActors,
            List<WaveActor> waveActors, List<ObstacleActor> obstacleActors) : base(WavesRecordEntryType.Information)
        {
            _map = map;
            _randomSeed = randomSeed;
            _maxTurns = maxTurns;
            _startTime = TimestampHelper.GetSimplifiedUTCTimestamp();
            _navalActors = navalActors;
            _waveActors = waveActors;
            _obstacleActors = obstacleActors;
            _sessionId = sessionId;
            _extraText = extraText;
            _deviceMode = SystemInfo.deviceModel;
            _processorType = SystemInfo.processorType;
            _graphicsDeviceName = SystemInfo.graphicsDeviceName;
        }

        public override void PerformEntry()
        {
            DebugUtils.DebugLogMsg($"Waves Game Info: {_map}. Random Seed: {_randomSeed}.",
                DebugUtils.DebugType.Temporary);
        }

        protected override string ToJson()
        {
            var json = JsonConvert.SerializeObject(new WavesGameInfoRecordEntryJson(
                    WavesRecordEntryTypeExtensions.WavesRecordEntryTypeToString(WavesRecordEntryType.Information), turn,
                    timeStamp, _startTime, _sessionId, _extraText, _map, _deviceMode, _processorType, _graphicsDeviceName,
                    _randomSeed, _maxTurns, _navalActors, _waveActors,
                    _obstacleActors),
                GetJsonSerializerSettings());
            return json;
        }
    }
}