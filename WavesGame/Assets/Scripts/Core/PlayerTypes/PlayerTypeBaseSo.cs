/*
 * Copyright (c) 2026 Yvens R Serpa [https://github.com/YvensFaos/]
 *
 * This work is licensed under the Creative Commons Attribution 4.0 International License.
 * To view a copy of this license, visit http://creativecommons.org/licenses/by/4.0/
 * or see the LICENSE file in the root directory of this repository.
 */

using System;
using System.Collections.Generic;
using Actors;
using UnityEngine;
using UUtils;

namespace Core.PlayerTypes
{
    [Serializable]
    public class FactionNavalShipPair : Pair<Faction, NavalShip>
    {
        public FactionNavalShipPair(Faction one, NavalShip two) : base(one, two)
        {
        }
    }
    
    public abstract class PlayerTypeBaseSo : ScriptableObject
    {
        [SerializeField]
        private List<FactionNavalShipPair> actorPairs;

        public abstract void InitializeType(NavalShip navalShip, HashSet<Faction> factions);
        
        public NavalShip GetActorFromFaction(Faction faction)
        {
            return actorPairs.Find(pair => pair.One.Equals(faction))?.Two;
        }

        public abstract string GetName();
    }
}