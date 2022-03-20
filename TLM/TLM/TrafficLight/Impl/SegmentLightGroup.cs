using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TrafficManager.API.Traffic.Enums;

namespace TrafficManager.TrafficLight.Impl {
    internal struct SegmentLightGroup : IEquatable<SegmentLightGroup> {
        public ExtVehicleType VehicleType;

        public SegmentLightGroup(ExtVehicleType vehicleType) {
            VehicleType = vehicleType;
        }

        public bool Equals(SegmentLightGroup other) {
            return VehicleType == other.VehicleType;
        }

        public override bool Equals(object obj) => obj is SegmentLightGroup other && Equals(other);

        public override int GetHashCode() {
            var prime = 31;
            int result = 1;
            result = prime * result + VehicleType.GetHashCode();
            return result;
        }

        public static bool operator ==(SegmentLightGroup left, SegmentLightGroup right) => left.Equals(right);

        public static bool operator !=(SegmentLightGroup left, SegmentLightGroup right) => !left.Equals(right);
    }
}
