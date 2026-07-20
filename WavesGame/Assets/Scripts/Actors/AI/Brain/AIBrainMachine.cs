/*
 * Copyright (c) 2026 Yvens R Serpa [https://github.com/YvensFaos/]
 *
 * This work is licensed under the Creative Commons Attribution 4.0 International License.
 * To view a copy of this license, visit http://creativecommons.org/licenses/by/4.0/
 * or see the LICENSE file in the root directory of this repository.
 */

using UnityEngine;

namespace Actors.AI.Brain
{
    public abstract class AIBrainMachine : ScriptableObject
    {
        public abstract bool CalculateMovement(AINavalShip aiNavalShip, int stepsAvailable,
            out AIGridUnitUtility moveTo);

        public abstract bool CalculateAttack(AINavalShip aiNavalShip, out AIGridUnitUtility attack);

        public abstract AIAction CalculateAction(AINavalShip aiNavalShip, int actionsAvailable, int stepsAvailable, out AIGridUnitUtility target);
    }
}