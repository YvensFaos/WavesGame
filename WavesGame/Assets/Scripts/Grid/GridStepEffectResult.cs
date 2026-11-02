/*
 * Copyright (c) 2026 Yvens R Serpa [https://github.com/YvensFaos/]
 *
 * This work is licensed under the MIT License.
 * To view a copy of this license, visit https://opensource.org/licenses/MIT
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

        public GridStepEffectResult(GridActor stepActor, bool canContinueMovement, GridUnit moveTo, bool causeDamage,
            int damage)
        {
            this.stepActor = stepActor;
            this.canContinueMovement = canContinueMovement;
            this.moveTo = moveTo;
            this.causeDamage = causeDamage;
            this.damage = damage;
        }
    }
}