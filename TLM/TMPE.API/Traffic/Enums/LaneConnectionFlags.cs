using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TrafficManager.API.Traffic.Enums {
    public enum LaneConnectionFlags {
        None = 0,
        CrossLeft = 1,
        CrossRight = 1 << 1,
        TurnOutOfDisplaced = 1 << 2,
        TurnIntoDisplaced = 1 << 3,
        ForwardDisplaced = 1 << 4,

        Computed = CrossLeft | CrossRight | TurnOutOfDisplaced | TurnIntoDisplaced | ForwardDisplaced,
    }
}
