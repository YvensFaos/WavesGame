/*
 * Copyright (c) 2026 Yvens R Serpa [https://github.com/YvensFaos/]
 *
 * This work is licensed under the MIT License.
 * To view a copy of this license, visit https://opensource.org/licenses/MIT
 * or see the LICENSE file in the root directory of this repository.
 */

using UnityEditor;
using UnityEngine;

namespace Core.Simulation.Editor
{
    [CustomEditor(typeof(SimulationManager))]
    public class SimulationManagerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            if (GUILayout.Button("Add simulations from folder..."))
            {
                var path = EditorUtility.OpenFolderPanel("Pick folder", "Assets/Data/Database/Simulations", "");
                if (string.IsNullOrEmpty(path)) return;

                var relative = "Assets" + path[Application.dataPath.Length..];
                var guids = AssetDatabase.FindAssets("t:Simulation", new[] { relative });

                var simulationManager = (SimulationManager)target;
                Undo.RecordObject(simulationManager, "Add simulations");
                foreach (var guid in guids)
                {
                    var assetPath = AssetDatabase.GUIDToAssetPath(guid);
                    var simulation = AssetDatabase.LoadAssetAtPath<Simulation>(assetPath);
                    if (simulation != null && !simulationManager.ContainsSimulation(simulation))
                        simulationManager.AddSimulation(simulation);
                }

                EditorUtility.SetDirty(simulationManager);
            }
        }
    }
}