/*
 * Copyright (c) 2026 Yvens R Serpa [https://github.com/YvensFaos/]
 *
 * This work is licensed under the MIT License.
 * To view a copy of this license, visit https://opensource.org/licenses/MIT
 * or see the LICENSE file in the root directory of this repository.
 */

using System;
using Actors;
using Core.PlayerTypes;
using UUtils;

namespace Core.Simulation
{
    [Serializable]
    public class FactionPlayerTypePair : Pair<Faction, PlayerTypeBaseSo>
    {
        public FactionPlayerTypePair(Faction one, PlayerTypeBaseSo two) : base(one, two)
        {
        }
    }
}