/*
 * Copyright (c) 2026 Yvens R Serpa [https://github.com/YvensFaos/]
 *
 * This work is licensed under the Creative Commons Attribution 4.0 International License.
 * To view a copy of this license, visit http://creativecommons.org/licenses/by/4.0/
 * or see the LICENSE file in the root directory of this repository.
 */

using Actors.Editor;
using UnityEditor;

namespace Actors.AI.Editor
{
    [CustomEditor(typeof(AINavalShip))]
    public class AINavalShipEditor : NavalShipEditor
    {
        private UnityEditor.Editor _navalScriptableObjectEditor;
        
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            
            var myTarget = (AINavalShip)target;
            var genes = myTarget.GenesData;
            if (genes == null) return;
            EditorGUILayout.Space(15);
            EditorGUILayout.HelpBox("Genes SO", MessageType.Info);
            CreateCachedEditor(genes, null, ref _navalScriptableObjectEditor);
            _navalScriptableObjectEditor.OnInspectorGUI();
        }
    }
}