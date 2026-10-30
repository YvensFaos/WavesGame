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
    public static class MenuSceneLoaderHelper
    {
        private const string CoreElementsScene = "CoreElementsScene";

        [MenuItem("Scenes/Load CoreElementsScene")]
        public static void LoadCoreElementsScene()
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
        
        [MenuItem("Scenes/Load Main Menu")]
        public static void LoadMainMenuScene()
        {
            var guids = AssetDatabase.FindAssets($"t:Scene MainMenu");
            if (guids.Length == 0)
            {
                DebugUtils.DebugLogErrorMsg($"Scene MainMenu not found in the project. Check the name.");
                return;
            }

            var scenePath = AssetDatabase.GUIDToAssetPath(guids[0]);
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                EditorSceneManager.OpenScene(scenePath);
            }
        }
        
        [MenuItem("Scenes/Play Main Scene %#m")]
        public static void PlayMainMenuScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            const string scenePath = "Assets/Scenes/MenuScene.unity";

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            EditorSceneManager.OpenScene(scenePath);
            EditorApplication.isPlaying = true;
        }
    }
}