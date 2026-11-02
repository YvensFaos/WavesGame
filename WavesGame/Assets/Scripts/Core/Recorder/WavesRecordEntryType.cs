/*
 * Copyright (c) 2026 Yvens R Serpa [https://github.com/YvensFaos/]
 *
 * This work is licensed under the MIT License.
 * To view a copy of this license, visit https://opensource.org/licenses/MIT
 * or see the LICENSE file in the root directory of this repository.
 */

using System;

namespace Core.Recorder
{
    public enum WavesRecordEntryType
    {
        Movement,
        Attack,
        Damage,
        Reasoning,
        Death,
        Goal,
        GameState,
        ScenarioState,
        StepFX,
        EndGame,
        Information,
        Command,
        Propagation,
        InvalidAttempt,
        Input,
        Turn
    }

    public static class WavesRecordEntryTypeExtensions
    {
        public static string WavesRecordEntryTypeToString(WavesRecordEntryType type)
        {
            return type switch
            {
                WavesRecordEntryType.Movement => "MOVE",
                WavesRecordEntryType.Attack => "ATTK",
                WavesRecordEntryType.Damage => "DAMG",
                WavesRecordEntryType.Death => "DEAD",
                WavesRecordEntryType.Goal => "GOAL",
                WavesRecordEntryType.EndGame => "OVER",
                WavesRecordEntryType.Information => "INFO",
                WavesRecordEntryType.Reasoning => "RESN",
                WavesRecordEntryType.InvalidAttempt => "NVLD",
                WavesRecordEntryType.Input => "INPT",
                WavesRecordEntryType.Command => "CMMD",
                WavesRecordEntryType.Turn => "TURN",
                WavesRecordEntryType.GameState => "GSTT",
                WavesRecordEntryType.ScenarioState => "SCNR",
                WavesRecordEntryType.Propagation => "PPGT",
                WavesRecordEntryType.StepFX => "STFX",
                _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
            };
        }
    }
}