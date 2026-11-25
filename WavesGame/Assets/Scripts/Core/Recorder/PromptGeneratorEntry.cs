using System;
using System.Collections.Generic;
using Grid;
using Newtonsoft.Json;
using UnityEngine;

namespace Core.Recorder
{
    [Serializable]
    public class PromptGeneratorEntryJson : ActorRecordEntryJson
    {
        [SerializeField] public SimpleVector2Int selfPosition;
        [SerializeField] public List<SimpleVector2Int> movementPositions;
        [SerializeField] public List<SimpleVector2Int> attackablePositions;
        [SerializeField] public List<SimpleVector2Int> enemyPositions;

        public PromptGeneratorEntryJson(string actorId, string faction, string eventType, int turn, long timeStamp,
            Vector2Int selfPosition,
            List<Vector2Int> movements, List<Vector2Int> attackables, List<Vector2Int> enemies)
            : base(actorId, faction, eventType, turn, timeStamp, "")
        {
            this.selfPosition = new SimpleVector2Int(selfPosition);
            movementPositions = new List<SimpleVector2Int>();
            movements.ForEach(x => movementPositions.Add(new SimpleVector2Int(x.x, x.y)));
            attackablePositions = new List<SimpleVector2Int>();
            attackables.ForEach(x => attackablePositions.Add(new SimpleVector2Int(x.x, x.y)));
            enemyPositions = new List<SimpleVector2Int>();
            enemies.ForEach(x => enemyPositions.Add(new SimpleVector2Int(x.x, x.y)));
        }
    }

    public class PromptGeneratorEntry : ActorRecordEntry
    {
        private readonly Vector2Int _selfPosition;
        private readonly List<Vector2Int> _movementPositions;
        private readonly List<Vector2Int> _attackablePositions;
        private readonly List<Vector2Int> _enemyPositions;

        public PromptGeneratorEntry(string actorId,
            Vector2Int position, List<GridUnit> movementGridPosition,
            List<GridActor> attackableGridPosition, List<GridUnit> enemyGridPosition, 
            Faction faction) : base(actorId, faction, WavesRecordEntryType.PromptInfo)
        {
            _selfPosition = position;
            _movementPositions = new List<Vector2Int>();
            movementGridPosition.ForEach(gridUnit => { _movementPositions.Add(gridUnit.Index()); });
            _attackablePositions = new List<Vector2Int>();
            attackableGridPosition.ForEach(gridActor => { _attackablePositions.Add(gridActor.GetUnit().Index()); });
            _enemyPositions =  new List<Vector2Int>();
            enemyGridPosition.ForEach(gridActor => { _enemyPositions.Add(gridActor.Index()); });
        }

        public override void PerformEntry()
        {
            //TODO
            throw new System.NotImplementedException();
        }

        protected override string ToJson()
        {
            return JsonConvert.SerializeObject(new PromptGeneratorEntryJson(ActorID, Faction,
                WavesRecordEntryTypeExtensions.WavesRecordEntryTypeToString(eventType), turn, timeStamp,
                _selfPosition, _movementPositions, _attackablePositions, _enemyPositions), GetJsonSerializerSettings());
        }
    }
}