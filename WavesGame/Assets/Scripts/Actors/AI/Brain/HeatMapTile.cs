/*
 * Copyright (c) 2026 Yvens R Serpa [https://github.com/YvensFaos/]
 *
 * This work is licensed under the Creative Commons Attribution 4.0 International License.
 * To view a copy of this license, visit http://creativecommons.org/licenses/by/4.0/
 * or see the LICENSE file in the root directory of this repository.
 */

using System.Collections.Generic;
using Lean.Pool;
using NaughtyAttributes;
using UnityEngine;

namespace Actors.AI.Brain
{
    public class HeatMapTile : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer heatTileSprite;
        [SerializeField] private Gradient heatGradient;

        // ReSharper disable once NotAccessedField.Local
        [SerializeField, ReadOnly] private float heat;
        [SerializeField, ReadOnly] private List<float> utilities;

        public void ShowHeatTile(float setHeat, List<float> setUtilities, float destroyAfter = 2.0f)
        {
            heat = setHeat;
            utilities = setUtilities;
            heatTileSprite.gameObject.SetActive(true);
            heatTileSprite.color = heatGradient.Evaluate(setHeat);
            if (destroyAfter > 0.0f)
            {
                LeanPool.Despawn(gameObject, destroyAfter);
            }
        }
    }
}