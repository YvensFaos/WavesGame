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
        [SerializeField] public List<NavalActorEntryJson> navalActorEntryJsons;

        public StateRecordEntryJson(string eventType, int turn, long timeStamp, List<NavalActor> navalActors)
            : base(eventType, turn, timeStamp)
        {
            navalActorEntryJsons = navalActors.Select(ship => new NavalActorEntryJson(ship)).ToList();
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