/*
 * Copyright (c) 2026 Yvens R Serpa [https://github.com/YvensFaos/]
 *
 * This work is licensed under the Creative Commons Attribution 4.0 International License.
 * To view a copy of this license, visit http://creativecommons.org/licenses/by/4.0/
 * or see the LICENSE file in the root directory of this repository.
 */

namespace Core.Recorder
{
    public abstract class ActorRecordEntry : WavesEntry
    {
        protected WavesRecordEntryType type;
        private string _comment;

        protected ActorRecordEntry(string actorId, WavesRecordEntryType type, int turn, long timeStamp) :
            base(WavesRecordEntryTypeTypeExtensions.WavesRecordEntryTypeToString(type), turn, timeStamp)
        {
            ActorID = actorId;
            _comment = "";
        }

        public void AppendComment(string comment)
        {
            _comment += $"{comment}";
        }

        protected virtual string Content()
        {
            return "";
        }

        public sealed override string ToString()
        {
            var commentLine = string.IsNullOrEmpty(_comment) ? "" : $";[{_comment}]";
            return
                $"{WavesRecordEntryTypeTypeExtensions.WavesRecordEntryTypeToString(type)};{ActorID}{Content()}{commentLine}";
        }

        protected string ActorID { get; }
    }
}