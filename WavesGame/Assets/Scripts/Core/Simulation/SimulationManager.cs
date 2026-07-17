/*
 * Copyright (c) 2025 Yvens R Serpa [https://github.com/YvensFaos/]
 *
 * This work is licensed under the Creative Commons Attribution 4.0 International License.
 * To view a copy of this license, visit http://creativecommons.org/licenses/by/4.0/
 * or see the LICENSE file in the root directory of this repository.
 */

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Actors;
using Core.PlayerTypes;
using NaughtyAttributes;
using UnityEngine;
using UnityEngine.SceneManagement;
using UUtils;

namespace Core.Simulation
{
    public class SimulationManager : MonoBehaviour
    {
        // [SerializeReference, Scene] private string battleGroundScene;
        [SerializeReference, Scene] private string controllerScene;

        [SerializeField] private List<Simulation> simulations;

        [SerializeField] private float warmUpTimer;

        private void Start()
        {
            StartCoroutine(SimulationCoroutine());
        }

        private IEnumerator SimulationCoroutine()
        {
            DebugUtils.DebugLogMsg($"Warming up simulation manager for {warmUpTimer} seconds...", DebugUtils.DebugType.System);
            yield return new WaitForSeconds(warmUpTimer);
            DebugUtils.DebugLogMsg("Simulation manager started!", DebugUtils.DebugType.System);

            yield return WaitUntilAsyncAdditiveLoadScene(controllerScene);
            DebugUtils.DebugLogMsg("Controller scene loaded!", DebugUtils.DebugType.System);
            yield return null;
            
            DebugUtils.DebugLogMsg($"Simulations on the list: {simulations.Count}", DebugUtils.DebugType.System);
            var internalCounter = 1;
            foreach (var simulation in simulations)
            {
                var iterations = simulation.Iterations;
                DebugUtils.DebugLogMsg($"Loading simulation {internalCounter}: {simulation.name}. Internal repetitions: {iterations}.", DebugUtils.DebugType.System);

                //Flags
                // swap positions
                
                for (var i = 1; i <= iterations; i++)
                {
                    DebugUtils.DebugLogMsg($"Iteration Number: {i}.", DebugUtils.DebugType.System);
                    DebugUtils.DebugLogMsg($"Loading simulation Battle Ground scene [{simulation.BattleGroundScene}]...", DebugUtils.DebugType.System);
                    yield return WaitUntilAsyncAdditiveLoadScene(simulation.BattleGroundScene);
                    DebugUtils.DebugLogMsg($"Battle Ground [{simulation.BattleGroundScene}] scene loaded!", DebugUtils.DebugType.System);

                    var placeholders =
                        FindObjectsByType<PlaceholderActor>(FindObjectsInactive.Include, FindObjectsSortMode.InstanceID)
                            .ToList();
                    DebugUtils.DebugLogMsg($"Placeholders found: {placeholders.Count}!", DebugUtils.DebugType.System);
                    placeholders.Sort();
                
                    var factionsDictionary = new Dictionary<Faction, List<PlaceholderActor>>();
                    foreach (var placeholder in placeholders)
                    {
                        if (!factionsDictionary.ContainsKey(placeholder.PlaceholderFaction))
                        {
                            factionsDictionary.Add(placeholder.PlaceholderFaction, new List<PlaceholderActor>());
                        }
                        factionsDictionary[placeholder.PlaceholderFaction].Add(placeholder);
                    }

                    foreach (var keyValuePair in factionsDictionary)
                    {
                        DebugUtils.DebugLogMsg($"Placeholders from {keyValuePair.Key} found: {keyValuePair.Value.Count}!", DebugUtils.DebugType.System);
                    }
                    
                    //Iterate over place holders to initialize ship prefabs
                    var factionPlayerTypes = simulation.FactionPlayerTypePairs;
                    foreach (var factionPlayerTypePair in factionPlayerTypes)
                    {
                        var faction = factionPlayerTypePair.One;
                        var playerType = factionPlayerTypePair.Two;
                        var factionList = factionsDictionary[faction];
                        InitializePlaceHoldersForFactionAndType(faction, playerType, factionList);
                    }
                }
                
                ++internalCounter;
            }
            
            //

            

           
            yield break;

            object WaitUntilAsyncAdditiveLoadScene(string sceneName)
            {
                DebugUtils.DebugLogMsg($"Additive loading scene: {sceneName}", DebugUtils.DebugType.System);
                var asyncOperation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
                return new WaitUntil(() => asyncOperation is { progress: >= 0.8f });
            }

            void InitializePlaceHoldersForFactionAndType(Faction faction, PlayerTypeBaseSo playerType,
                List<PlaceholderActor> placeHolders)
            {
                
            }
        }
    }
}