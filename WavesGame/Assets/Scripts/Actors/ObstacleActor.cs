/*
 * Copyright (c) 2026 Yvens R Serpa [https://github.com/YvensFaos/]
 *
 * This work is licensed under the MIT License.
 * To view a copy of this license, visit https://opensource.org/licenses/MIT
 * or see the LICENSE file in the root directory of this repository.
 */

using Grid;
using UUtils;

namespace Actors
{
    public class ObstacleActor : GridActor
    {
        protected override void Start()
        {
            DelayHelper.DelayOneFrame(this, () =>
            {
                base.Start();
                maxHealth = 1;
                currentHealth = maxHealth;
                destructible = false;
                blockGridUnit = true;
                hasStepEffect = false;
            });
        }
    }
}