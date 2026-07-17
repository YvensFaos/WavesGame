/*
 * Copyright (c) 2025 Yvens R Serpa [https://github.com/YvensFaos/]
 *
 * This work is licensed under the Creative Commons Attribution 4.0 International License.
 * To view a copy of this license, visit http://creativecommons.org/licenses/by/4.0/
 * or see the LICENSE file in the root directory of this repository.
 */

using System.Collections;
using Actors;
using NaughtyAttributes;
using UnityEngine;
using UnityEngine.SceneManagement;
using UUtils;

namespace Core
{
    public class SimulationManager : MonoBehaviour
    {
        [SerializeReference, Scene]
        private string battleGroundScene;
        [SerializeReference, Scene]
        private string controllerScene;

        [SerializeField]
        private float warmUpTimer;

        private void Start()
        {
            StartCoroutine(SimulationCoroutine());
        }

        private IEnumerator SimulationCoroutine()
        {
            DebugUtils.DebugLogMsg($"Warming up simulation for {warmUpTimer} seconds...", DebugUtils.DebugType.System);
            yield return new WaitForSeconds(warmUpTimer);
            DebugUtils.DebugLogMsg("Simulation started!", DebugUtils.DebugType.System);
            
            yield return WaitUntilAsyncAdditiveLoadScene(battleGroundScene);
            yield return WaitUntilAsyncAdditiveLoadScene(controllerScene);
            
            DebugUtils.DebugLogMsg("Additive scenes loaded!", DebugUtils.DebugType.System);
            yield return null;
            
            //TODO get all positions from the battle ground scene
            var placeholders = GameObject.FindObjectsByType<PlaceholderActor>(FindObjectsInactive.Include, FindObjectsSortMode.InstanceID);
            DebugUtils.DebugLogMsg($"Placeholders found: {placeholders}!", DebugUtils.DebugType.System);
            
            
            yield break;

            object WaitUntilAsyncAdditiveLoadScene(string sceneName)
            {
                DebugUtils.DebugLogMsg($"Additive loading scene: {sceneName}", DebugUtils.DebugType.System);
                var asyncOperation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
                return new WaitUntil(() => asyncOperation is { progress: >= 0.8f });
            }
        }
    }
}