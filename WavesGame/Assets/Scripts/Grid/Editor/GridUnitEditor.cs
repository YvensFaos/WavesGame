/*
 * Copyright (c) 2026 Yvens R Serpa [https://github.com/YvensFaos/]
 *
 * This work is licensed under the MIT License.
 * To view a copy of this license, visit https://opensource.org/licenses/MIT
 * or see the LICENSE file in the root directory of this repository.
 */

using UnityEditor;
using UnityEngine;

namespace Grid.Editor
{
    [CustomEditor(typeof(GridUnit))]
    public class GridUnitEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            var gridUnit = (GridUnit)target;

            if (GridManager.GetSingleton() != null)
            {
                var sprite = GridManager.GetSingleton().GetSpriteForType(gridUnit.Type());
                EditorGUIUtility.SetIconForObject(gridUnit.gameObject, sprite.texture);
            }

            if (!gridUnit.HasValidActors()) return;
            var enumerator = gridUnit.GetActorEnumerator();
            GUILayout.Label($"Actors [{gridUnit.ActorsCount()}]:");
            while (enumerator.MoveNext())
            {
                if (enumerator.Current == null) continue;
                EditorGUILayout.BeginHorizontal();
                var current = enumerator.Current;
                GUILayout.Label($"{current.name}");
                if (GUILayout.Button("Ping", GUILayout.Width(40)))
                {
                    Selection.activeObject = current;
                    EditorGUIUtility.PingObject(current);
                }

                EditorGUILayout.EndHorizontal();
            }
            enumerator.Dispose();
        }
    }
}