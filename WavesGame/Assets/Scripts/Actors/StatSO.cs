/*
 * Copyright (c) 2026 Yvens R Serpa [https://github.com/YvensFaos/]
 *
 * This work is licensed under the MIT License.
 * To view a copy of this license, visit https://opensource.org/licenses/MIT
 * or see the LICENSE file in the root directory of this repository.
 */

using NaughtyAttributes;
using UnityEngine;

namespace Actors
{
    [CreateAssetMenu(fileName = "New Stat", menuName = "Waves/Stat", order = 2)]
    public class StatSO : ScriptableObject
    {
        public string statName;
        [ShowAssetPreview] public Sprite statIcon;
        [TextArea(3, 10)] public string statDescription;
    }
}