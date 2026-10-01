/*
 * Copyright (c) 2026 Yvens R Serpa [https://github.com/YvensFaos/]
 *
 * This work is licensed under the Creative Commons Attribution 4.0 International License.
 * To view a copy of this license, visit http://creativecommons.org/licenses/by/4.0/
 * or see the LICENSE file in the root directory of this repository.
 */

using NaughtyAttributes;
using UnityEngine;

namespace Actors.AI.Brain
{
    public class HeatMapTile : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer heatTileSprite;
        [SerializeField] private Gradient heatGradient;

        [SerializeField, ReadOnly] private float heat;

        public void ShowHeatTile(float setHeat, float destroyAfter = 0.0f)
        {
            heat = setHeat;
            heatTileSprite.gameObject.SetActive(true);
            heatTileSprite.color = heatGradient.Evaluate(setHeat);
            if (destroyAfter > 0.0f)
            {
                Destroy(gameObject, destroyAfter);
            }
        }
    }
}