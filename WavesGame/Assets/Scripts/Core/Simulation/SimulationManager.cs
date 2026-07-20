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
        [Header("Simulation References")] [SerializeReference, Scene]
        private string controllerScene;

        [SerializeField] private SimulationController simulationControllerPrefab;

        [Header("Simulations")] [SerializeField]
        private List<Simulation> simulations;

        [Header("Simulation Settings")] [SerializeField]
        private SimulationFlags flags;

        [SerializeField] private float warmUpTimer;
        [SerializeField] private int simulationSeed = 6;
        [SerializeField, ReadOnly] private Faction firstFaction;

        private void Start()
        {
            StartCoroutine(SimulationCoroutine());
        }

        private IEnumerator SimulationCoroutine()
        {
            DebugUtils.DebugLogMsg($"Warming up simulation manager for {warmUpTimer} seconds...",
                DebugUtils.DebugType.System);
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
                DebugUtils.DebugLogMsg(
                    $"Loading simulation {internalCounter}: {simulation.name}. Internal repetitions: {iterations}.",
                    DebugUtils.DebugType.System);
                for (var i = 1; i <= iterations; i++)
                {
                    yield return RunSimulation(i, simulation);
                }

                ++internalCounter;
            }

            DebugUtils.DebugLogMsg($"All simulations completed!", DebugUtils.DebugType.System);
            
            yield return WaitUntilAsyncAdditiveUnloadScene(controllerScene);
            DebugUtils.DebugLogMsg("Controller scene unloaded!", DebugUtils.DebugType.System);
            yield return null;
            
            DebugUtils.DebugLogMsg("Quit application.", DebugUtils.DebugType.System);
            ApplicationHelper.QuitApplication();
        }

        private IEnumerator RunSimulation(int i1, Simulation simulation)
        {
            {
                DebugUtils.DebugLogMsg($"Iteration Number: {i1}.", DebugUtils.DebugType.System);

                DebugUtils.DebugLogMsg(
                    $"Loading simulation Battle Ground scene [{simulation.BattleGroundScene}]...",
                    DebugUtils.DebugType.System);

                yield return WaitUntilAsyncAdditiveLoadScene(simulation.BattleGroundScene);

                DebugUtils.DebugLogMsg($"Battle Ground [{simulation.BattleGroundScene}] scene loaded!",
                    DebugUtils.DebugType.System);

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
                    DebugUtils.DebugLogMsg(
                        $"Placeholders from {keyValuePair.Key} found: {keyValuePair.Value.Count}!",
                        DebugUtils.DebugType.System);
                }

                //Iterate over placeholders to initialize ship prefabs
                var factionPlayerTypes = simulation.FactionPlayerTypePairs;
                var factionNavalShipsDictionary = new Dictionary<Faction, List<NavalShip>>();
                var factionsHash = new HashSet<Faction>();
                foreach (var keyValuePair in factionPlayerTypes)
                {
                    factionsHash.Add(keyValuePair.One);
                }

                foreach (var factionPlayerTypePair in factionPlayerTypes)
                {
                    var faction = factionPlayerTypePair.One;
                    var playerType = factionPlayerTypePair.Two;
                    var factionList = factionsDictionary[faction];
                    var navalShips =
                        InitializePlaceHoldersForFactionAndType(faction, playerType, factionList, factionsHash);
                    factionNavalShipsDictionary.Add(faction, navalShips);
                }
                PerformFlags(factionNavalShipsDictionary);

                var simulationController = Instantiate(simulationControllerPrefab);
                simulationController.Initialize(factionNavalShipsDictionary);

                DebugUtils.DebugLogMsg($"Starting simulation...", DebugUtils.DebugType.System);
                yield return simulationController.StartSimulation(simulationSeed);
                DebugUtils.DebugLogMsg($"Simulation completed!", DebugUtils.DebugType.System);

                var outcome = simulationController.Outcome;
                var winningString = "";
                if (outcome == SimulationOutcome.Victory)
                {
                    var winningFaction = simulationController.WinningFaction;
                    winningString = $"Winning Faction: {winningFaction}";
                }

                DebugUtils.DebugLogMsg($"Result: {outcome}.{winningString}", DebugUtils.DebugType.System);
                Destroy(simulationController.gameObject);

                yield return new WaitForSeconds(warmUpTimer);

                DebugUtils.DebugLogMsg(
                    $"Unloading simulation Battle Ground scene [{simulation.BattleGroundScene}]...",
                    DebugUtils.DebugType.System);
                yield return WaitUntilAsyncAdditiveUnloadScene(simulation.BattleGroundScene);
                DebugUtils.DebugLogMsg($"Battle Ground [{simulation.BattleGroundScene}] scene unloaded!",
                    DebugUtils.DebugType.System);

                yield return new WaitForSeconds(warmUpTimer);

                //Destroy remaining actors
                var remainingActors = 0;
                DebugUtils.DebugLogMsg($"Deleting remaining actors...", DebugUtils.DebugType.System);
                foreach (var placeholder in factionNavalShipsDictionary.SelectMany(keyValuePair =>
                             keyValuePair.Value))
                {
                    if (placeholder == null) continue;
                    Destroy(placeholder.gameObject);
                    remainingActors++;
                }

                DebugUtils.DebugLogMsg($"{remainingActors} actors removed!", DebugUtils.DebugType.System);
                yield return new WaitForSeconds(warmUpTimer);
            }
        }

        private static List<NavalShip> InitializePlaceHoldersForFactionAndType(Faction faction, PlayerTypeBaseSo playerType,
            List<PlaceholderActor> placeHolders, HashSet<Faction> factionsHash)
        {
            var navalShips = new List<NavalShip>();

            //Sort by the order
            placeHolders.Sort();

            var baseNavalActor = playerType.GetActorFromFaction(faction);
            for (var i = placeHolders.Count - 1; i >= 0; --i)
            {
                var placeHolder = placeHolders[i];
                var navalShip = Instantiate(baseNavalActor, placeHolder.transform.position, Quaternion.identity);
                navalShip.ConfigureID(placeHolder.Order);
                navalShip.UpdateFaction(faction);

                //Initialize the naval ship according to its specific player type
                playerType.InitializeType(navalShip, factionsHash);

                navalShips.Add(navalShip);
                DebugUtils.DebugLogMsg($"Naval Ship Added: {navalShip.name}", DebugUtils.DebugType.System);
                Destroy(placeHolder.gameObject);
            }

            return navalShips;
        }
        
        private static IEnumerator WaitUntilAsyncAdditiveLoadScene(string sceneName)
        {
            DebugUtils.DebugLogMsg($"Additive loading scene: {sceneName}", DebugUtils.DebugType.System);
            var asyncOperation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            yield return asyncOperation;
            yield return new WaitUntil(() => asyncOperation is { isDone: true });
        }

        private static IEnumerator WaitUntilAsyncAdditiveUnloadScene(string sceneName)
        {
            DebugUtils.DebugLogMsg($"Additive unloading scene: {sceneName}", DebugUtils.DebugType.System);
            var asyncOperation = SceneManager.UnloadSceneAsync(sceneName);
            if (asyncOperation == null)
            {
                DebugUtils.DebugLogErrorMsg($"Scene '{sceneName}' not found or already unloaded.");
                yield break;
            }

            yield return asyncOperation;
            yield return new WaitUntil(() => asyncOperation is { isDone: true });
        }

        private void PerformFlags(Dictionary<Faction, List<NavalShip>> dictionary)
        {
            var factions = dictionary.Keys.ToList();

            ChangeFactionOrder();
            InterleaveFactionOrder();
            return;

            void ChangeFactionOrder()
            {
                if (!flags.HasFlag(SimulationFlags.ChangeFactionOrder)) return;
                if (firstFaction != null)
                {
                    factions.Remove(firstFaction);
                    factions.Add(firstFaction);
                }

                firstFaction = factions[0];
            }

            void InterleaveFactionOrder()
            {
                if (!flags.HasFlag(SimulationFlags.InterleavedOrder)) return;
                var indices = new Pair<Faction, int>[factions.Count];
                for (var i = 0; i < factions.Count; i++)
                {
                    indices[i] = new Pair<Faction, int>(factions[i], 0);
                }

                var order = 1;
                int skip;
                do
                {
                    skip = 0;
                    foreach (var pair in indices)
                    {
                        if (pair.Two < dictionary[pair.One].Count)
                        {
                            var navalShip = dictionary[pair.One][pair.Two];
                            if (navalShip != null) navalShip.SetInitiative(order++);
                            else skip++;
                            pair.Two++;
                        }
                        else
                        {
                            skip++;
                        }
                    }
                } while (skip < indices.Length - 1);
            }
        }
    }
}