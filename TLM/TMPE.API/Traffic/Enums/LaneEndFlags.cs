using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TrafficManager.API.Traffic.Enums {
    [Flags]
    public enum LaneEndFlags {
        None = 0,
        CrossLeft = 1,
        CrossRight = 1 << 1,
        TurnOutOfDisplaced = 1 << 2,
        TurnIntoDisplaced = 1 << 3,
        ForwardDisplaced = 1 << 4,

        Displacement = CrossLeft | CrossRight | TurnOutOfDisplaced | TurnIntoDisplaced | ForwardDisplaced,

        Initialized = 1 << 31,
    }
}
