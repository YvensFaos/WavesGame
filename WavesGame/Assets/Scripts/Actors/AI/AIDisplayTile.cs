/*
 * Copyright (c) 2026 Yvens R Serpa [https://github.com/YvensFaos/]
 *
 * This work is licensed under the MIT License.
 * To view a copy of this license, visit https://opensource.org/licenses/MIT
 * or see the LICENSE file in the root directory of this repository.
 */

using DG.Tweening;
using UnityEngine;

namespace Actors.AI
{
    public class AIDisplayTile : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer tileSprite;

        private readonly Color _transparent = new(1, 1, 1, 0);

        private void OnEnable()
        {
            tileSprite.DOColor(_transparent, 0.5f).From();
        }
    }
}