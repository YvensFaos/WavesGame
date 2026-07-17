/*
 * Copyright (c) 2025 Yvens R Serpa [https://github.com/YvensFaos/]
 *
 * This work is licensed under the Creative Commons Attribution 4.0 International License.
 * To view a copy of this license, visit http://creativecommons.org/licenses/by/4.0/
 * or see the LICENSE file in the root directory of this repository.
 */

using System;
using NaughtyAttributes;
using UnityEngine;

namespace Actors
{
    [RequireComponent(typeof(SpriteRenderer))]
    public class PlaceholderActor : MonoBehaviour, IComparable<PlaceholderActor>
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

        public int CompareTo(PlaceholderActor other)
        {
            if (ReferenceEquals(this, other)) return 0;
            if (other is null) return 1;
            return placeholderFaction.Equals(other.PlaceholderFaction)
                ? order.CompareTo(other.Order)
                : string.Compare(placeholderFaction.name, other.PlaceholderFaction.name, StringComparison.Ordinal);
        }
    }
}