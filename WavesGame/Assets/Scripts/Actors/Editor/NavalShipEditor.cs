/*
 * Copyright (c) 2026 Yvens R Serpa [https://github.com/YvensFaos/]
 *
 * This work is licensed under the MIT License.
 * To view a copy of this license, visit https://opensource.org/licenses/MIT
 * or see the LICENSE file in the root directory of this repository.
 */

using Grid.Editor;
using UnityEditor;
using UnityEngine;

namespace Actors.Editor
{
    [CustomEditor(typeof(NavalShip))]
    public class NavalShipEditor : GridActorEditor
    {
        private UnityEditor.Editor _scriptableObjectEditor;

        public override void OnInspectorGUI()
        {
            var myTarget = (NavalShip)target;
            //TODO change the colour
            GUILayout.Label(
                $"Init: [{myTarget.Initiative}] - Step: [{myTarget.RemainingSteps}/{myTarget.ShipData.stats.speed}] - Acts: [{myTarget.ActionsLeft}]");
            var grid = myTarget.GetUnit();
            GUILayout.Label($"Index [{(grid == null ? -1 : grid.Index().ToString())}]", EditorStyles.boldLabel);

            base.OnInspectorGUI();

            if (GUILayout.Button("Update Ship Graphics"))
            {
                myTarget.Renderer().sprite = myTarget.ShipData.shipSprite;
            }

            var navalShipSo = myTarget.ShipData;
            if (navalShipSo == null) return;
            EditorGUILayout.Space(15);
            EditorGUILayout.HelpBox("Naval Ship SO", MessageType.Info);
            CreateCachedEditor(navalShipSo, null, ref _scriptableObjectEditor);
            _scriptableObjectEditor.OnInspectorGUI();
        }
    }
}