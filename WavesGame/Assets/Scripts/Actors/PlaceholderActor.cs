/*
 * Copyright (c) 2026 Yvens R Serpa [https://github.com/YvensFaos/]
 *
 * This work is licensed under the MIT License.
 * To view a copy of this license, visit https://opensource.org/licenses/MIT
 * or see the LICENSE file in the root directory of this repository.
 */

using System;
using Core;
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
        [SerializeField] private bool overrideCurrentHealth;

        [SerializeField, ShowIf("overrideCurrentHealth")]
        private int currentHealth;

        [Button("Update Color")]
        private void UpdateSpriteColorToFactionColor()
        {
            placeholderRenderer.color = placeholderFaction.factionColor;
        }

        [Button("Snap to Grid")]
        private void SnapToGrid()
        {
            transform.position = new Vector3(SnapHalf(transform.position.x), SnapHalf(transform.position.y), 0.0f);
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

        public void ProcessNavalShip(NavalShip navalShip)
        {
            if (overrideCurrentHealth)
            {
                navalShip.OverrideCurrentHealth(currentHealth);
            }
        }
        
        private static float SnapHalf(float v) => Mathf.Round(v - 0.5f) + 0.5f;
    }
}