using System;
using Actors;
using UUtils;

namespace Core
{
    [Serializable]
    public class LevelActorPair : Pair<NavalShip, bool>, IComparable<LevelActorPair>
    {
        public LevelActorPair(NavalShip one) : base(one, true)
        {
        }

        public static implicit operator bool(LevelActorPair pair) => pair.One != null && pair.Two;

        public int CompareTo(LevelActorPair other)
        {
            return other.One.Initiative.CompareTo(other.One.Initiative);
        }
    }
}