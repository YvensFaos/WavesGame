using System.Collections.Generic;
using Actors;
using UnityEngine;

namespace Core.PlayerTypes
{
    [CreateAssetMenu(fileName = "Human Player Type", menuName = "Waves/Player Type/Human Player", order = 3)]
    public class HumanPlayerType : PlayerTypeBaseSo
    {
        public override void InitializeType(NavalShip navalShip, HashSet<Faction> factions)
        {
            //TODO do something here
        }

        public override string GetName()
        {
            return "Human";
        }
    }
}