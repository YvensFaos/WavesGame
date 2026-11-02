/*
 * Copyright (c) 2025 Yvens R Serpa [https://github.com/YvensFaos/]
 *
 * This work is licensed under the Creative Commons Attribution 4.0 International License.
 * To view a copy of this license, visit http://creativecommons.org/licenses/by/4.0/
 * or see the LICENSE file in the root directory of this repository.
 */

using System;

namespace Grid
{
    [Serializable]
    public struct GridStepEffectResult
    {
        public GridActor stepActor;
        public bool canContinueMovement;
        public GridUnit moveTo;
        public bool causeDamage;
        public int damage;

        public GridStepEffectResult(GridActor stepActor, bool canContinueMovement, GridUnit moveTo, bool causeDamage, int damage)
        {
            this.stepActor = stepActor;
            this.canContinueMovement = canContinueMovement;
            this.moveTo = moveTo;
            this.causeDamage = causeDamage;
            this.damage = damage;
        }
    }
}