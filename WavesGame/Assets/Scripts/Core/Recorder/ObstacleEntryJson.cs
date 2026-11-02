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