/*
 * Copyright (c) 2025 Yvens R Serpa [https://github.com/YvensFaos/]
 *
 * This work is licensed under the Creative Commons Attribution 4.0 International License.
 * To view a copy of this license, visit http://creativecommons.org/licenses/by/4.0/
 * or see the LICENSE file in the root directory of this repository.
 */

using NaughtyAttributes;
using UnityEngine;

namespace Actors
{
    [RequireComponent(typeof(SpriteRenderer))]
    public class PlaceholderActor : MonoBehaviour
    {
        [SerializeField] private int order;
        [SerializeField] private Faction placeholderFaction;
        [SerializeField] private SpriteRenderer placeholderRenderer;

        [Button("Update Color")]
        private void UpdateSpriteColorToFactionColor()
        {
            placeholderRenderer.color = placeholderFaction.factionColor;
        }

        public int Order => order;
        public Faction PlaceholderFaction => placeholderFaction;
    }
}