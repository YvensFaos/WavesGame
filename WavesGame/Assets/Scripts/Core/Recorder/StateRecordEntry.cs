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
using UnityEngine;
using UUtils;

namespace Core.Recorder
{
    [Serializable]
    public class StateRecordEntryJson : WavesEntryJson
    {
        [SerializeField] public List<NavalActorShortEntryJson> navalActorEntryJsons;

        public StateRecordEntryJson(string eventType, int turn, long timeStamp, List<NavalActor> navalActors)
            : base(eventType, turn, timeStamp)
        {
            navalActors = navalActors.FindAll(actor => actor != null);
            navalActorEntryJsons = navalActors.Select(ship => new NavalActorShortEntryJson(ship)).ToList();
        }
    }

    public class StateRecordEntry : WavesEntry
    {
        private readonly List<NavalActor> _navalActors;

        public StateRecordEntry(List<NavalActor> navalActors) : base(WavesRecordEntryType.GameState)
        {
            _navalActors = navalActors;
        }

        public override void PerformEntry()
        {
            DebugUtils.DebugLogMsg($"Waves Game State: {_navalActors.Count} actors.", DebugUtils.DebugType.Temporary);
        }

        protected override string ToJson()
        {
            return JsonUtility.ToJson(new StateRecordEntryJson(
                WavesRecordEntryTypeExtensions.WavesRecordEntryTypeToString(WavesRecordEntryType.GameState), turn,
                timeStamp, _navalActors));
        }
    }
}