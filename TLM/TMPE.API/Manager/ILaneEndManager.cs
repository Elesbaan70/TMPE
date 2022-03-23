using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TrafficManager.API.Traffic.Enums;

namespace TrafficManager.API.Manager {
    public interface ILaneEndManager {
        /// <summary>
        /// Returns the flags for this lane end.
        /// </summary>
        /// <param name="laneId">The Lane ID</param>
        /// <param name="startNode">Start Node</param>
        /// <returns>Flags</returns>
        LaneEndFlags GetFlags(uint laneId, bool startNode);
    }
}
