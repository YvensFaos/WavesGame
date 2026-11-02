/*
 * Copyright (c) 2025 Yvens R Serpa [https://github.com/YvensFaos/]
 *
 * This work is licensed under the Creative Commons Attribution 4.0 International License.
 * To view a copy of this license, visit http://creativecommons.org/licenses/by/4.0/
 * or see the LICENSE file in the root directory of this repository.
 */

using UnityEngine;

namespace Actors
{
    [CreateAssetMenu(fileName = "New Faction", menuName = "Waves/Faction", order = 1)]
    public class Faction : ScriptableObject
    {
        public Color factionColor;
        private static Faction neutral;
        
        public override string ToString()
        {
            return name;
        }

        public static Faction GetNeutralFaction()
        {
            if (neutral != null) return neutral;
            neutral = CreateInstance<Faction>();
            neutral.factionColor = Color.white;
            neutral.name = "Neutral";
            return neutral;
        }
    }
}