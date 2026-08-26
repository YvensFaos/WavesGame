/*
 * Copyright (c) 2026 Yvens R Serpa [https://github.com/YvensFaos/]
 *
 * This work is licensed under the Creative Commons Attribution 4.0 International License.
 * To view a copy of this license, visit http://creativecommons.org/licenses/by/4.0/
 * or see the LICENSE file in the root directory of this repository.
 */

using System.Collections.Generic;
using NaughtyAttributes;
using UnityEngine;

namespace Core.Simulation
{
    [CreateAssetMenu(fileName = "Simulation", menuName = "Waves/Simulation", order = 0)]
    public class Simulation : ScriptableObject
    {
        [SerializeField] private int iterations;
        [SerializeReference, Scene] private string battleGroundScene;
        [SerializeField] private bool record = true;
        [SerializeField] private List<FactionPlayerTypePair> factionPlayerTypePairs;

        public string BattleGroundScene => battleGroundScene;

        public List<FactionPlayerTypePair> FactionPlayerTypePairs => factionPlayerTypePairs;

        public int Iterations => iterations;
        public bool Record => record;
    }
}