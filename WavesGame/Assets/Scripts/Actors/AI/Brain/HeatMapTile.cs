/*
 * Copyright (c) 2026 Yvens R Serpa [https://github.com/YvensFaos/]
 *
 * This work is licensed under the Creative Commons Attribution 4.0 International License.
 * To view a copy of this license, visit http://creativecommons.org/licenses/by/4.0/
 * or see the LICENSE file in the root directory of this repository.
 */

using UnityEngine;
using UUtils;

public class HeatMapTile : MonoBehaviour
{
    [SerializeField]
    private SpriteRenderer heatTileSprite;
    [SerializeField]
    private Gradient heatGradient;
    
    public void ShowHeatTile(float intensity, float destroyAfter = 0.0f)
    {
        heatTileSprite.gameObject.SetActive(true);
        DebugUtils.DebugLogMsg($"Intensity: {intensity}.", DebugUtils.DebugType.Temporary);
        heatTileSprite.color = heatGradient.Evaluate(intensity);
        if (destroyAfter > 0.0f)
        {
            Destroy(gameObject, destroyAfter);
        }
    }
}
