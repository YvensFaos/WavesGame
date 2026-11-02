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

namespace Core.Simulation.Editor
{
    [CustomEditor(typeof(Simulation))]
    [CanEditMultipleObjects]
    public class SimulationEditor : UnityEditor.Editor
    {
        private UnityEditor.Editor _scriptableObjectEditor;
        
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space(10);
            if (GUILayout.Button("Ping in Project"))
            {
                EditorGUIUtility.PingObject(target);
                Selection.activeObject = target;
            }
            var simulation = (Simulation) target;
            if (GUILayout.Button(
                    targets.Length > 1
                        ? $"Rename {targets.Length} Simulations"
                        : "Rename Simulation"))
            {
                if (targets.Length > 1)
                {
                    foreach (var t in targets)
                    {
                        var singleSimulation = t as Simulation;
                        if (singleSimulation != null)
                            RenameScriptableObjectHelper.RenameAssetFile(singleSimulation, singleSimulation.ToString());
                    }
                }
                else
                {
                    RenameScriptableObjectHelper.RenameAssetFile(simulation, simulation.ToString());
                }
            }
            if (targets.Length > 1)
            {
                EditorGUILayout.HelpBox(
                    $"{targets.Length} Simulations selected. " +
                    "Select a single Simulation to edit its faction pairs.",
                    MessageType.Info);
                return;
            }
            
            EditorGUILayout.Space(15);
            var simulationFactionPlayerTypePairs = simulation.FactionPlayerTypePairs;
            foreach (var pair in simulationFactionPlayerTypePairs)
            {
				if(pair == null || pair.One == null || pair.Two == null) return;
                var rect = EditorGUILayout.BeginVertical();
                var faction = pair.One;
                EditorGUI.DrawRect(rect, faction.factionColor);
                CreateCachedEditor(pair.Two, null, ref _scriptableObjectEditor);
                _scriptableObjectEditor.OnInspectorGUI();
                EditorGUILayout.EndVertical();
            }
        }
    }
}