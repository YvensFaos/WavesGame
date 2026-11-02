/*
 * Copyright (c) 2026 Yvens R Serpa [https://github.com/YvensFaos/]
 *
 * This work is licensed under the MIT License.
 * To view a copy of this license, visit https://opensource.org/licenses/MIT
 * or see the LICENSE file in the root directory of this repository.
 */

using Core;
using UnityEngine;
using UnityEngine.Events;
using UUtils;

namespace Actors
{
    public class NavalTarget : NavalActor
    {
        [SerializeField] private NavalActorStats stats;
        [SerializeField] private UnityEvent eventOnDestruction;

        public override bool TakeDamage(int damage)
        {
            var damageTaken = damage - stats.sturdiness.Two;
            DebugUtils.DebugLogMsg(
                $"{name} attacked with {damage}. {stats.sturdiness}. Damage taken was {damageTaken}.",
                DebugUtils.DebugType.Verbose);
            return base.TakeDamage(damageTaken);
        }

        protected override void NotifyGameController()
        {
            GameController.TryToUseGameController(controller => { controller.NotifyDestroyedActor(this); });
        }

        protected override void DestroyActor()
        {
            base.DestroyActor();
            eventOnDestruction.Invoke();
        }

        public NavalActorStats GetStats() => stats;

        public override string ToString()
        {
            return $"{base.ToString()}; stats={stats}";
        }
    }
}