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