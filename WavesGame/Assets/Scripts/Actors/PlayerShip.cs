/*
 * Copyright (c) 2026 Yvens R Serpa [https://github.com/YvensFaos/]
 *
 * This work is licensed under the Creative Commons Attribution 4.0 International License.
 * To view a copy of this license, visit http://creativecommons.org/licenses/by/4.0/
 * or see the LICENSE file in the root directory of this repository.
 */

using UnityEngine;
using UUtils;

namespace Actors
{
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerShip : NavalShip
    {
        [SerializeField] private Rigidbody2D playerBody;
        [SerializeField] private GameObject currentTurnVisual;

        protected override void Awake()
        {
            base.Awake();
            AssessUtils.CheckRequirement(ref playerBody, this);
        }

        public override void StartTurn()
        {
            base.StartTurn();
            currentTurnVisual.SetActive(true);
        }

        public override void EndTurn()
        {
            base.EndTurn();
            currentTurnVisual.SetActive(false);
        }
    }
}