/*
 * Copyright (c) 2026 Yvens R Serpa [https://github.com/YvensFaos/]
 *
 * This work is licensed under the MIT License.
 * To view a copy of this license, visit https://opensource.org/licenses/MIT
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