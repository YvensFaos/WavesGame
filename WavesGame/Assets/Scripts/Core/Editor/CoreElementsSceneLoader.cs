/*
 * Copyright (c) 2026 Yvens R Serpa [https://github.com/YvensFaos/]
 *
 * This work is licensed under the Creative Commons Attribution 4.0 International License.
 * To view a copy of this license, visit http://creativecommons.org/licenses/by/4.0/
 * or see the LICENSE file in the root directory of this repository.
 */

using UnityEditor;
using UnityEditor.SceneManagement;
using UUtils;

namespace Core.Editor
{
    public static class CoreElementsSceneLoader
    {
        private const string CoreElementsScene = "CoreElementsScene";

        [MenuItem("Scenes/Load CoreElementsScene")]
        public static void LoadMainScene()
        {
            var guids = AssetDatabase.FindAssets($"t:Scene {CoreElementsScene}");
            if (guids.Length == 0)
            {
                DebugUtils.DebugLogErrorMsg($"Scene '{CoreElementsScene}' not found in the project. Check the name.");
                return;
            }

            var scenePath = AssetDatabase.GUIDToAssetPath(guids[0]);
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                EditorSceneManager.OpenScene(scenePath);
            }
        }
    }
}