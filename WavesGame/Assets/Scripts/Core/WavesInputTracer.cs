/*
 * Copyright (c) 2026 Yvens R Serpa [https://github.com/YvensFaos/]
 *
 * This work is licensed under the MIT License.
 * To view a copy of this license, visit https://opensource.org/licenses/MIT
 * or see the LICENSE file in the root directory of this repository.
 */

using Actors;
using Core.Recorder;
using DCF;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;

namespace Core
{
    public class WavesInputTracer : GenericInputTracer
    {
        private NavalShip _currentNavalShipActor;

        public void SetCurrentNavalShip(NavalShip navalShip)
        {
            _currentNavalShipActor = navalShip;
        }

        protected override void ProcessActionEventPtr(InputActionTrace.ActionEventPtr actionEventPtr)
        {
            if (actionEventPtr.phase is not InputActionPhase.Performed) return;
            if (!WavesRecorder.TryToGetSingleton(out var recorder)) return;
            if (_currentNavalShipActor == null)
            {
                recorder.RecordNewEntry(new InputRecordEntry(actionEventPtr));
            }
            else
            {
                recorder.RecordNewEntry(new ActorInputRecordEntry(_currentNavalShipActor.name,
                    _currentNavalShipActor.GetFaction(), _currentNavalShipActor.HumanControlled, actionEventPtr));
            }
        }
    }
}