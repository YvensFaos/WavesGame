/*
 * Copyright (c) 2026 Yvens R Serpa [https://github.com/YvensFaos/]
 *
 * This work is licensed under the Creative Commons Attribution 4.0 International License.
 * To view a copy of this license, visit http://creativecommons.org/licenses/by/4.0/
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