/*
 * Copyright (c) 2026 Yvens R Serpa [https://github.com/YvensFaos/]
 *
 * This work is licensed under the MIT License.
 * To view a copy of this license, visit https://opensource.org/licenses/MIT
 * or see the LICENSE file in the root directory of this repository.
 */

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