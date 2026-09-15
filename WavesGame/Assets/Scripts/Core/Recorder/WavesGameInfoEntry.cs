/*
 * Copyright (c) 2026 Yvens R Serpa [https://github.com/YvensFaos/]
 *
 * This work is licensed under the Creative Commons Attribution 4.0 International License.
 * To view a copy of this license, visit http://creativecommons.org/licenses/by/4.0/
 * or see the LICENSE file in the root directory of this repository.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using Actors;
using Core.Recorder.Extras;
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
        [SerializeField] public List<NavalActorEntryJson> navalActorEntryJsons;
        [SerializeField] public List<WaveActorEntryJson> waveActorEntryJsons;
        [SerializeField] public List<ObstacleEntryJson> obstacleEntryJsons;

        public WavesGameInfoRecordEntryJson(string eventType, int turn, long timeStamp, string map, int randomSeed,
            int maxTurns,
            List<NavalActor> navalActors, List<WaveActor> waveActors, List<ObstacleActor> obstacleActors) : base(
            eventType, turn, timeStamp)
        {
            this.map = map;
            this.randomSeed = randomSeed;
            this.maxTurns = maxTurns;
            navalActorEntryJsons = navalActors.Select(ship => new NavalActorEntryJson(ship)).ToList();
            waveActorEntryJsons = waveActors.Select(waveActor => new WaveActorEntryJson(waveActor)).ToList();
            obstacleEntryJsons = obstacleActors.Select(obstacleActor => new ObstacleEntryJson(obstacleActor)).ToList();
        }
    }

    public class WavesGameInfoEntry : WavesEntry
    {
        private readonly string _map;
        private readonly int _randomSeed;
        private readonly int _maxTurns;
        private readonly List<NavalActor> _navalActors;
        private readonly List<WaveActor> _waveActors;
        private readonly List<ObstacleActor> _obstacleActors;

        public WavesGameInfoEntry(string map, int randomSeed, int maxTurns, List<NavalActor> navalActors,
            List<WaveActor> waveActors, List<ObstacleActor> obstacleActors) : base(
            WavesRecordEntryType.Information)
        {
            _map = map;
            _randomSeed = randomSeed;
            _maxTurns = maxTurns;
            _navalActors = navalActors;
            _waveActors = waveActors;
            _obstacleActors = obstacleActors;
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
                timeStamp, _map, _randomSeed, _maxTurns, _navalActors, _waveActors, _obstacleActors), GetJsonSerializerSettings());
            return json;
        }
    }
}