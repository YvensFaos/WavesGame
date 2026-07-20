using Grid;
using UnityEngine;

namespace Actors.AI.Brain
{
    public abstract class AIBrainMachine : ScriptableObject
    {
        public abstract bool CalculateMovement(AINavalShip aiNavalShip, int stepsAvailable,
            out AIGridUnitUtility moveTo);

        public abstract bool CalculateAttack(AINavalShip aiNavalShip, out AIGridUnitUtility attack);

        public abstract AIAction CalculateAction(AINavalShip aiNavalShip, int actionsAvailable, int stepsAvailable, out AIGridUnitUtility target);
    }
}