/*
 * Copyright (c) 2026 Yvens R Serpa [https://github.com/YvensFaos/]
 *
 * This work is licensed under the MIT License.
 * To view a copy of this license, visit https://opensource.org/licenses/MIT
 * or see the LICENSE file in the root directory of this repository.
 */

using UnityEngine;
using UUtils;

namespace Actors.Cannon
{
    public class BaseCannon : MonoBehaviour
    {
        [SerializeField] private CannonSo cannonData;

        public CannonSo GetCannonSo => cannonData;

        public int CalculateDamage()
        {
            return cannonData.damage + DiceHelper.RollDiceFromString(cannonData.damageDie);
        }

        public string GetCannonDataName()
        {
            return cannonData.name;
        }

        public override string ToString()
        {
            return $"Cannon: {cannonData}";
        }
    }
}