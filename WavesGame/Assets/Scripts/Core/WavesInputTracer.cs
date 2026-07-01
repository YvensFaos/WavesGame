using Core.Recorder;
using DCF;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;

namespace Core
{
    public class WavesInputTracer : GenericInputTracer
    {
        protected override void ProcessActionEventPtr(InputActionTrace.ActionEventPtr actionEventPtr)
        {
            if (actionEventPtr.phase is not InputActionPhase.Performed) return;
            if (!WavesRecorder.TryToGetSingleton(out var recorder)) return;
            if (!LevelController.TryToGetSingleton(out var levelController)) return;
            recorder.RecordNewEntry(new InputRecordEntry(actionEventPtr, levelController.GetTurn(), levelController
                .GetTimeStamp()));
        }
    }
}