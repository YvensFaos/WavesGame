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

namespace Actors.AI.Brain.Editor
{
    [CustomEditor(typeof(AIBrainMachine))]
    public class AIBrainMachineEditor: UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space(10);
            var aiBrainMachine = (AIBrainMachine) target;
            if (GUILayout.Button("Rename AIBrainMachine"))
            {
                RenameScriptableObjectHelper.RenameAssetFile(aiBrainMachine, aiBrainMachine.ToString());
            }
        }
    }
}