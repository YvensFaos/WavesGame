using System.Collections.Generic;
using Actors;
using UUtils;

namespace Core.Recorder
{
    public class WavesGameInfoEntry : WavesEntry
    {
        private const string WavesGameInfoRecordType = "INFO";
        private readonly string _map;
        private readonly int _randomSeed;
        private List<NavalActor> _ships;
        
        public WavesGameInfoEntry(string map, int randomSeed, List<NavalActor> ships) : base(WavesGameInfoRecordType, 0, -1)
        {
            _map = map;
            _randomSeed = randomSeed;
            _ships = ships;
        }

        public override void PerformEntry()
        {
            DebugUtils.DebugLogMsg($"Waves Game Info: {_map}. Random Seed: {_randomSeed}.", DebugUtils.DebugType.Temporary);
        }

        public sealed override string ToString()
        {
            return $"{WavesGameInfoRecordType};Waves Game Info: {_map}. Random Seed: {_randomSeed}. Ships: {_ships.Count}.";
        }

        public override string ToJson()
        {
            throw new System.NotImplementedException();
        }
    }
}