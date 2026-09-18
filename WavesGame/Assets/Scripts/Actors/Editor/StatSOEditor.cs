/*
 * Copyright (c) 2026 Yvens R Serpa [https://github.com/YvensFaos/]
 * 
 * This work is licensed under the Creative Commons Attribution 4.0 International License.
 * To view a copy of this license, visit http://creativecommons.org/licenses/by/4.0/
 * or see the LICENSE file in the root directory of this repository.
 */

using UnityEditor;
using UnityEngine;
using UUtils.Editor;

namespace Actors.Editor
{
    [CustomEditor(typeof(StatSO))]
    // ReSharper disable once InconsistentNaming
    public class StatSOEditor : CustomIconEditor<StatSO>
    {
        protected override bool CheckValidSpriteInfo(StatSO scriptableObject)
        {
            return scriptableObject.statIcon != null;
        }

        protected override Sprite GetSprite(StatSO scriptableObject)
        {
            return scriptableObject.statIcon;
        }
    }
}