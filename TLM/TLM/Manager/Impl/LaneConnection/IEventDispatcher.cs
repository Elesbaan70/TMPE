using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TrafficManager.Manager.Impl.LaneConnection {
    internal interface IEventDispatcher {

        void ConnectionsChanged(uint laneId, bool startNode);
    }
}
