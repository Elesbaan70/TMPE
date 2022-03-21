using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TrafficManager.API.Traffic.Enums;

namespace TrafficManager.TrafficLight.Impl {
    internal struct SegmentLightGroup : IEquatable<SegmentLightGroup> {

        public ExtVehicleType VehicleType;
        public LaneEndFlags LaneEndFlags;

        public const LaneEndFlags LANE_END_FLAGS_GROUPING = LaneEndFlags.Displacement;

        public SegmentLightGroup(ExtVehicleType vehicleType) {
            VehicleType = vehicleType;
            LaneEndFlags = LaneEndFlags.None;
        }

        public SegmentLightGroup(ExtVehicleType vehicleType, LaneEndFlags laneEndFlags) {
            VehicleType = vehicleType;
            LaneEndFlags = laneEndFlags & LANE_END_FLAGS_GROUPING;
        }

        public SegmentLightGroup ForVehicleType(ExtVehicleType vehicleType) => new SegmentLightGroup(vehicleType, LaneEndFlags);

        public bool HasExtendedGrouping() => new SegmentLightGroup(VehicleType) != this;

        public override string ToString() {
            return $"[SegmentLightGroup VehicleType=[{VehicleType}], LaneEndFlags=[{LaneEndFlags}]]";
        }

        public bool Equals(SegmentLightGroup other) {
            return VehicleType == other.VehicleType && LaneEndFlags == other.LaneEndFlags;
        }

        public override bool Equals(object obj) => obj is SegmentLightGroup other && Equals(other);

        public override int GetHashCode() {
            var prime = 31;
            int result = 1;
            result = prime * result + VehicleType.GetHashCode();
            result = prime * result + LaneEndFlags.GetHashCode();
            return result;
        }

        public static bool operator ==(SegmentLightGroup left, SegmentLightGroup right) => left.Equals(right);

        public static bool operator !=(SegmentLightGroup left, SegmentLightGroup right) => !left.Equals(right);
    }
}
