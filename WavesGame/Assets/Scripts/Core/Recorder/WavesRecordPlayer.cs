using UUtils;
using UUtils.GameRecorder;

namespace Core.Recorder
{
    public class WavesRecordPlayer : GamePlayer
    {
        protected override void Start()
        {
            //First stop the LevelController
            if (playOnStart)
            {
                LevelController.GetSingleton().StopLevel();
            }
            base.Start();
        }

        public override void StartPlayingRecord(string file)
        {
            LevelController.GetSingleton().StopLevel();
            base.StartPlayingRecord(file);
        }

        protected override void ExecuteEntry(string entryText)
        {
            var parts = entryText.Split(";");
            var entryType = parts[0];
            ActorRecordEntry entry = null;
            try
            {
                switch (entryType)
                {
                    case "MOVE":
                        entry = MovementRecordEntry.MakeRecordEntryFromString(entryText);
                        if (entry == null) throw new EntryConversionException(entryText, typeof(MovementRecordEntry));
                        break;
                    case "ATTK":
                        entry = AttackRecordEntry.MakeRecordEntryFromString(entryText);
                        if(entry == null) throw new EntryConversionException(entryText, typeof(AttackRecordEntry));
                        break;
                    case "DAMG":
                        entry = DamageRecordEntry.MakeRecordEntryFromString(entryText);
                        if(entry == null) throw new EntryConversionException(entryText, typeof(DamageRecordEntry));
                        break;
                    case "DEAD":
                        entry = DeathRecordEntry.MakeRecordEntryFromString(entryText);
                        if(entry == null) throw new EntryConversionException(entryText, typeof(DeathRecordEntry));
                        break;
                }
            }
            catch (EntryConversionException entryConversionException)
            {
                DebugUtils.DebugLogException(entryConversionException);
            }

            entry?.PerformEntry();
        }
    }
}