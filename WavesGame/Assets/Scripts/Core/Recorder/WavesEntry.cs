/*
 * Copyright (c) 2026 Yvens R Serpa [https://github.com/YvensFaos/]
 *
 * This work is licensed under the Creative Commons Attribution 4.0 International License.
 * To view a copy of this license, visit http://creativecommons.org/licenses/by/4.0/
 * or see the LICENSE file in the root directory of this repository.
 */

using UUtils.GameRecorder;

namespace Core.Recorder
{
    public abstract class WavesEntry : RecordEntry
    {
        protected string eventType;
        protected int turn;
        protected long timeStamp;

        protected WavesEntry(string eventType, int turn, long timeStamp)
        {
            this.eventType = eventType;
            this.turn = turn;
            this.timeStamp = timeStamp;
        }

        public abstract string ToJson();
    }
}