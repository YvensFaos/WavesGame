/*
 * Copyright (c) 2026 Yvens R Serpa [https://github.com/YvensFaos/]
 *
 * This work is licensed under the Creative Commons Attribution 4.0 International License.
 * To view a copy of this license, visit http://creativecommons.org/licenses/by/4.0/
 * or see the LICENSE file in the root directory of this repository.
 */

using System;

namespace Core.Simulation
{
    [Flags]
    public enum SimulationFlags
    {
        None = 0,
        InterleavedOrder = 1,
        ChangeFactionOrder = 2,
        //System = 2,
        //Temporary = 4,
        //Warning = 8,
        //Error = 16,
        //Verbose = 32
    }
}