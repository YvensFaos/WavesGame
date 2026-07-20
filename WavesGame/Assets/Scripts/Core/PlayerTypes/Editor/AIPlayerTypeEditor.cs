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

namespace Core.PlayerTypes.Editor
{
    [CustomEditor(typeof(AIPlayerType))]
    public class AIPlayerTypeEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space(10);
            var aiPlayerType = (AIPlayerType) target;
            if (GUILayout.Button("Rename AI Player Type"))
            {
                RenameScriptableObjectHelper.RenameAssetFile(aiPlayerType, aiPlayerType.GetName());
            }
        }
    }
}