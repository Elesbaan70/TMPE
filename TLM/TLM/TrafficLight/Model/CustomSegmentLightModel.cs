using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TrafficManager.API.Traffic.Enums;

namespace TrafficManager.TrafficLight.Model {
    internal class CustomSegmentLightModel {

        public SegmentLightGroup Key;

        public LightMode CurrentMode;

        public RoadBaseAI.TrafficLightState LightLeft;

        public RoadBaseAI.TrafficLightState LightMain;

        public RoadBaseAI.TrafficLightState LightRight;
    }
}
