/*
 * Copyright (c) 2026 Yvens R Serpa [https://github.com/YvensFaos/]
 *
 * This work is licensed under the MIT License.
 * To view a copy of this license, visit https://opensource.org/licenses/MIT
 * or see the LICENSE file in the root directory of this repository.
 */

using System.Collections.Generic;
using Actors;
using UnityEngine;

namespace Core.PlayerTypes
{
    [CreateAssetMenu(fileName = "Human Player Type", menuName = "Waves/Player Type/Human Player", order = 3)]
    public class HumanPlayerType : PlayerTypeBaseSo
    {
        public override void InitializeType(NavalShip navalShip, HashSet<Faction> factions)
        {
            navalShip.UpdateName();
            navalShip.HumanControlled = true;
        }

        public override string GetName()
        {
            return "H";
        }
    }
}