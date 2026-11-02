/*
 * Copyright (c) 2026 Yvens R Serpa [https://github.com/YvensFaos/]
 *
 * This work is licensed under the MIT License.
 * To view a copy of this license, visit https://opensource.org/licenses/MIT
 * or see the LICENSE file in the root directory of this repository.
 */

using UnityEngine;
using UnityEngine.Events;
using UUtils;

namespace Core
{
    public class GameEventTrigger : AgnosticCollider2D
    {
        [SerializeField] private UnityEvent eventTrigger;

        protected override void Solve(GameObject collidedWith)
        {
            eventTrigger.Invoke();
        }
    }
}