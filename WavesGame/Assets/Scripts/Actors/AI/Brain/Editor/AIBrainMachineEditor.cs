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

namespace Actors.AI.Brain.Editor
{
    [CustomEditor(typeof(AIBrainMachine))]
    public class AIBrainMachineEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space(10);
            var aiBrainMachine = (AIBrainMachine)target;
            if (GUILayout.Button("Rename AIBrainMachine"))
            {
                RenameScriptableObjectHelper.RenameAssetFile(aiBrainMachine, aiBrainMachine.ToString());
            }
        }
    }
}