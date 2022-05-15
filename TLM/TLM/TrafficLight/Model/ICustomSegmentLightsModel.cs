using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TrafficManager.API.Traffic.Enums;

namespace TrafficManager.TrafficLight.Model {
    internal interface ICustomSegmentLightsModel : IEnumerable<CustomSegmentLightModel> {

        ushort NodeId { get; }

        ushort SegmentId { get; }

        RoadBaseAI.TrafficLightState? PedestrianLightState { get; }

        bool ManualPedestrianMode { get; }
    }
}
