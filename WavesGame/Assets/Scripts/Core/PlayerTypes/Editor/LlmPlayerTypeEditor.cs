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
    [CustomEditor(typeof(LlmPlayerType))]
    public class LlmPlayerTypeEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space(10);
            var llmPlayerType = (LlmPlayerType) target;
            
            if (GUILayout.Button("Rename LLM Player Type"))
            {
                RenameScriptableObjectHelper.RenameAssetFile(llmPlayerType, llmPlayerType.GetName());
            }
            
            EditorGUILayout.Space(10);
            // ReSharper disable once InvertIf
            if (GUILayout.Button("Ping in Project"))
            {
                EditorGUIUtility.PingObject(target);
                Selection.activeObject = target;
            }
        }
    }
}