using CSUtil.Commons;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using TrafficManager.Manager.Impl;

namespace TrafficManager.Util.Extensions {
    public static class NetInfoExtensions {

        public static bool IsAnyDisplaced(this NetInfo.Lane[] lanes) {
            if (lanes != null) {
                float minForwardPos = float.MaxValue;
                float minBackwardPos = float.MaxValue;
                float maxForwardPos = float.MinValue;
                float maxBackwardPos = float.MinValue;
                for (int i = 0; i < lanes.Length; i++) {
                    var lane = lanes[i];
                    if (lane?.IsDrivingLane() == true) {
                        if (lane.IsForward()) {
                            if (lane.m_position < minForwardPos) {
                                minForwardPos = lane.m_position;
                            }
                            if (lane.m_position > maxForwardPos) {
                                maxForwardPos = lane.m_position;
                            }
                        }
                        if (lane.IsBackward()) {
                            if (lane.m_position < minBackwardPos) {
                                minBackwardPos = lane.m_position;
                            }
                            if (lane.m_position > maxBackwardPos) {
                                maxBackwardPos = lane.m_position;
                            }
                        }
                    }
                }
                return (minForwardPos < maxBackwardPos) ^ (minBackwardPos > maxForwardPos);
            }
            return false;
        }

        public static bool IsDisplaced(this NetInfo.Lane lane, NetInfo.Lane[] allLanes) {
            if (lane?.IsDrivingLane() == true) {
                float minForwardPos = float.MaxValue;
                float minBackwardPos = float.MaxValue;
                float maxForwardPos = float.MinValue;
                float maxBackwardPos = float.MinValue;
                for (int i = 0; i < allLanes.Length; i++) {
                    var othrLane = allLanes[i];
                    if (othrLane?.IsDrivingLane() == true) {
                        if (othrLane.IsForward()) {
                            if (othrLane.m_position < minForwardPos) {
                                minForwardPos = othrLane.m_position;
                            }
                            if (othrLane.m_position > maxForwardPos) {
                                maxForwardPos = othrLane.m_position;
                            }
                        }
                        if (othrLane.IsBackward()) {
                            if (othrLane.m_position < minBackwardPos) {
                                minBackwardPos = othrLane.m_position;
                            }
                            if (othrLane.m_position > maxBackwardPos) {
                                maxBackwardPos = othrLane.m_position;
                            }
                        }
                    }
                }
                if (lane.IsForward() && lane.m_position < maxBackwardPos) {
                    if (maxForwardPos > (lane.m_position < minBackwardPos ? minBackwardPos : maxBackwardPos)) {
                        return true;
                    }
                }
                if (lane.IsBackward() && lane.m_position > minForwardPos) {
                    if (minBackwardPos < (lane.m_position > maxForwardPos ? maxForwardPos : minForwardPos)) {
                        return true;
                    }
                }
            }
            return false;
        }

        public static bool IsDrivingLane(this NetInfo.Lane lane) {
            return (lane.m_laneType & LaneArrowManager.LANE_TYPES) != 0
                    && (lane.m_vehicleType & LaneArrowManager.VEHICLE_TYPES) != 0;
        }

        public static bool IsForward(this NetInfo.Lane lane) {
            return (lane.m_direction & NetInfo.Direction.Forward) != 0
                    && (lane.m_direction & NetInfo.Direction.AvoidForward & NetInfo.Direction.Avoid) == 0;
        }

        public static bool IsBackward(this NetInfo.Lane lane) {
            return (lane.m_direction & NetInfo.Direction.Backward) != 0
                    && (lane.m_direction & NetInfo.Direction.AvoidBackward & NetInfo.Direction.Avoid) == 0;
        }

        public static bool HasDisplacedLanes(this NetInfo info) => info.Ext().HasDisplacedLanes;

        public static bool IsDisplacedLane(this NetInfo info, int laneIndex) => info.Ext().IsDisplacedLane(laneIndex);

        private static readonly ReaderWriterLockSlim rwLock = new ReaderWriterLockSlim();
        private static readonly Dictionary<NetInfo, ExtNetInfo> extNetInfos = new(ReferenceEqualityComparer<NetInfo>.Instance);

        private static ExtNetInfo Ext(this NetInfo netInfo) {
            rwLock.EnterUpgradeableReadLock();
            try {
                if (!extNetInfos.TryGetValue(netInfo, out var result)) {

                    rwLock.EnterWriteLock();
                    try {
                        result = extNetInfos[netInfo] = new ExtNetInfo(netInfo);
                    }
                    finally {
                        rwLock.ExitWriteLock();
                    }
                }
                return result;
            }
            finally {
                rwLock.ExitUpgradeableReadLock();
            }
        }

        private class ExtNetInfo {
            private readonly NetInfo info;
            private readonly object lockObj = new object();

            public ExtNetInfo(NetInfo info) {
                this.info = info;
            }

            private bool? _hasDisplacedLanes;
            public bool HasDisplacedLanes {
                get {
                    if (!_hasDisplacedLanes.HasValue) {
                        lock (lockObj) { // This can create a race condition, so...
                            if (!_hasDisplacedLanes.HasValue) { // we check it again. It's cheap.

                                _hasDisplacedLanes = info.m_lanes.IsAnyDisplaced();
                            }
                        }
                    }
                    return _hasDisplacedLanes.Value;
                }
            }

            private bool?[] displacedLanes;
            public bool IsDisplacedLane(int laneIndex) {
                displacedLanes ??= new bool?[info.m_lanes.Length];
                var result = displacedLanes[laneIndex];

                if (!result.HasValue) {
                    lock (lockObj) { // This can create a race condition, so...
                        if (!result.HasValue) { // we check it again. It's cheap.

                            displacedLanes[laneIndex] = result = info.m_lanes?[laneIndex]?.IsDisplaced(info.m_lanes) ?? false;
                        }
                    }
                }
                return result.Value;
            }
        }
    }
}