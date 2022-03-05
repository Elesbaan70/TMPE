using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TMUnitTest.Util.Extensions.NetInfoExtensions {
    internal static class NetInfoTestUtil {
        public static NetInfo.Lane Lane(NetInfo.Direction direction, float position) {
            return new NetInfo.Lane {
                m_laneType = NetInfo.LaneType.Vehicle,
                m_vehicleType = VehicleInfo.VehicleType.Car,
                m_direction = direction,
                m_position = position,
            };
        }
    }
}