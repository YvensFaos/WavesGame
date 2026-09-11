namespace Core.Recorder
{
    public class StepEffectEntry : WavesEntry
    {
        public StepEffectEntry(WavesRecordEntryType eventType) : base(eventType)
        {
        }

        public override void PerformEntry()
        {
            throw new System.NotImplementedException();
        }
    }
}