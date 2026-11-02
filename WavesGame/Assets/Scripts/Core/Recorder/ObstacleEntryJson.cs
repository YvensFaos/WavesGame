/*
 * Copyright (c) 2026 Yvens R Serpa [https://github.com/YvensFaos/]
 *
 * This work is licensed under the MIT License.
 * To view a copy of this license, visit https://opensource.org/licenses/MIT
 * or see the LICENSE file in the root directory of this repository.
 */

using System;
using Actors;
using UnityEngine;

namespace Core.Recorder
{
    [Serializable]
    public class ObstacleEntryJson
    {
        [SerializeField] public string name;
        [SerializeField] public SimpleVector2Int position;
        [SerializeField] public int health;
        [SerializeField] public bool destructible;

        public ObstacleEntryJson(ObstacleActor obstacleActor)
        {
            name = obstacleActor.name;
            position = new SimpleVector2Int(obstacleActor.GetUnit().Index());
            health = obstacleActor.GetMaxHealth();
            destructible = false;
        }
    }
}