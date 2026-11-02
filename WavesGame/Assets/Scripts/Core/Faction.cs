/*
 * Copyright (c) 2026 Yvens R Serpa [https://github.com/YvensFaos/]
 *
 * This work is licensed under the MIT License.
 * To view a copy of this license, visit https://opensource.org/licenses/MIT
 * or see the LICENSE file in the root directory of this repository.
 */

using UnityEngine;

namespace Core
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