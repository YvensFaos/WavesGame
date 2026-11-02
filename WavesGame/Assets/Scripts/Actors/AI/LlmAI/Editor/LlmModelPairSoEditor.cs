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

namespace Actors.AI.LlmAI.Editor
{
    [CustomEditor(typeof(LlmModelPairSo))]
    public class LlmModelPairSoEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space(10);
            var llmModelPairSo = (LlmModelPairSo)target;
            if (GUILayout.Button("Rename LlmModelPair"))
            {
                RenameScriptableObjectHelper.RenameAssetFile(llmModelPairSo, llmModelPairSo.modelPair.ToString());
            }
        }
    }
}