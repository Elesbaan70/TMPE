// #define DEBUGFLAGS // uncomment to print verbose log.

namespace TrafficManager.State {
    using ColossalFramework;
    using CSUtil.Commons;
    using System.Collections.Generic;
    using System;
    using TrafficManager.API.Traffic.Enums;
    using TrafficManager.Manager.Impl;
    using TrafficManager.Manager.Impl.LaneConnection;
    using TrafficManager.Util.Extensions;

    // [Obsolete] // I commented this on out for now to prevent warning spam
    // [issue #1476] make flags obsolete.
    public class Flags {
        /// <summary>
        /// For each lane: Defines the allowed vehicle types
        /// </summary>
        internal static ExtVehicleType?[][] laneAllowedVehicleTypesArray; // for faster, lock-free access, 1st index: segment id, 2nd index: lane index

        static Flags() {
            laneAllowedVehicleTypesArray = new ExtVehicleType?[NetManager.MAX_SEGMENT_COUNT][];
        }

        /// <summary>Called from Debug Panel.</summary>
        internal static void PrintDebugInfo() {
            Log.Info("---------------------------------");
            Log.Info("--- LANE VEHICLE RESTRICTIONS ---");
            Log.Info("---------------------------------");
            for (uint i = 0; i < laneAllowedVehicleTypesArray.Length; ++i) {
                ref NetSegment netSegment = ref ((ushort)i).ToSegment();

                if (laneAllowedVehicleTypesArray[i] == null)
                    continue;
                Log.Info($"Segment {i}: valid? {netSegment.IsValid()}");
                for (int x = 0; x < laneAllowedVehicleTypesArray[i].Length; ++x) {
                    if (laneAllowedVehicleTypesArray[i][x] == null)
                        continue;
                    Log.Info($"\tLane idx {x}: {laneAllowedVehicleTypesArray[i][x]}");
                }
            }
        }

        [Obsolete]
        public static bool MayHaveTrafficLight(ushort nodeId) {
            if (nodeId <= 0) {
                return false;
            }

            ref NetNode node = ref nodeId.ToNode();

            if ((node.m_flags &
                 (NetNode.Flags.Created | NetNode.Flags.Deleted)) != NetNode.Flags.Created)
            {
                // Log._Debug($"Flags: Node {nodeId} may not have a traffic light (not created).
                // flags={node.m_flags}");
                node.m_flags &= ~NetNode.Flags.TrafficLights;
                return false;
            }

            if (node.CountSegments() < 2) {
                // ignore dead-ends.
                return false;
            }


            if ((node.m_flags & NetNode.Flags.Untouchable) != NetNode.Flags.None
                && (node.m_flags & NetNode.Flags.LevelCrossing) != NetNode.Flags.None){
                // untouchable & level_crossing - Movable Bridges mod nodes, not allowed to be controlled by TMPE
                return false;
            }

            ItemClass connectionClass = node.Info.GetConnectionClass();
            if ((node.m_flags & NetNode.Flags.Junction) == NetNode.Flags.None &&
                connectionClass.m_service != ItemClass.Service.PublicTransport)
            {
                // Log._Debug($"Flags: Node {nodeId} may not have a traffic light (no junction or
                // not public transport). flags={node.m_flags}
                // connectionClass={connectionClass?.m_service}");
                node.m_flags &= ~NetNode.Flags.TrafficLights;
                return false;
            }

            if (connectionClass == null ||
                (connectionClass.m_service != ItemClass.Service.Road &&
                 connectionClass.m_service != ItemClass.Service.PublicTransport))
            {
                // Log._Debug($"Flags: Node {nodeId} may not have a traffic light (no connection class).
                // connectionClass={connectionClass?.m_service}");
                node.m_flags &= ~NetNode.Flags.TrafficLights;
                return false;
            }

            return true;
        }

        [Obsolete]
        public static bool SetNodeTrafficLight(ushort nodeId, bool flag) {
            if (nodeId <= 0) {
                return false;
            }

#if DEBUGFLAGS
            Log._Debug($"Flags: Set node traffic light: {nodeId}={flag}");
#endif

            if (!MayHaveTrafficLight(nodeId)) {
                //Log.Warning($"Flags: Refusing to add/delete traffic light to/from node: {nodeId} {flag}");
                return false;
            }

            ref NetNode node = ref nodeId.ToNode();
            NetNode.Flags flags = node.m_flags | NetNode.Flags.CustomTrafficLights;
            if (flag) {
#if DEBUGFLAGS
                Log._Debug($"Adding traffic light @ node {nId}");
#endif
                flags |= NetNode.Flags.TrafficLights;
            } else {
#if DEBUGFLAGS
                Log._Debug($"Removing traffic light @ node {nId}");
#endif
                flags &= ~NetNode.Flags.TrafficLights;
            }

            node.m_flags = flags;

            return true;
        }

        internal static bool CheckLane(uint laneId) {
            // TODO refactor
            if (laneId <= 0) {
                return false;
            }

            ref NetLane netLane = ref laneId.ToLane();

            if (((NetLane.Flags)netLane.m_flags & (NetLane.Flags.Created | NetLane.Flags.Deleted)) != NetLane.Flags.Created) {
                return false;
            }

            ushort segmentId = netLane.m_segment;
            if (segmentId <= 0) {
                return false;
            }

            ref NetSegment netSegment = ref segmentId.ToSegment();

            return (netSegment.m_flags & (NetSegment.Flags.Created | NetSegment.Flags.Deleted)) == NetSegment.Flags.Created;
        }

        public static void SetLaneAllowedVehicleTypes(uint laneId, ExtVehicleType vehicleTypes) {
            if (laneId <= 0) {
                return;
            }

            ref NetLane netLane = ref laneId.ToLane();

            if (((NetLane.Flags)netLane.m_flags & (NetLane.Flags.Created | NetLane.Flags.Deleted)) != NetLane.Flags.Created) {
                return;
            }

            ushort segmentId = netLane.m_segment;

            if (segmentId <= 0) {
                return;
            }

            ref NetSegment netSegment = ref segmentId.ToSegment();

            if ((netSegment.m_flags & (NetSegment.Flags.Created | NetSegment.Flags.Deleted)) != NetSegment.Flags.Created) {
                return;
            }

            NetInfo segmentInfo = netSegment.Info;
            uint curLaneId = netSegment.m_lanes;
            uint laneIndex = 0;

            while (laneIndex < segmentInfo.m_lanes.Length && curLaneId != 0u) {
                if (curLaneId == laneId) {
                    SetLaneAllowedVehicleTypes(segmentId, laneIndex, laneId, vehicleTypes);
                    return;
                }

                laneIndex++;
                curLaneId = curLaneId.ToLane().m_nextLane;
            }
        }

        public static void SetLaneAllowedVehicleTypes(ushort segmentId,
                                                      uint laneIndex,
                                                      uint laneId,
                                                      ExtVehicleType vehicleTypes)
        {
            if (segmentId <= 0 || laneId <= 0) {
                return;
            }

            ref NetSegment netSegment = ref segmentId.ToSegment();

            if ((netSegment.m_flags & (NetSegment.Flags.Created | NetSegment.Flags.Deleted)) != NetSegment.Flags.Created) {
                return;
            }

            ref NetLane netLane = ref laneId.ToLane();

            if (((NetLane.Flags)netLane.m_flags & (NetLane.Flags.Created | NetLane.Flags.Deleted)) != NetLane.Flags.Created) {
                return;
            }

            NetInfo segmentInfo = netSegment.Info;

            if (laneIndex >= segmentInfo.m_lanes.Length) {
                return;
            }

#if DEBUGFLAGS
            Log._Debug("Flags.setLaneAllowedVehicleTypes: setting allowed vehicles of lane index " +
                       $"{laneIndex} @ seg. {segmentId} to {vehicleTypes.ToString()}");
#endif

            // save allowed vehicle types into the fast-access array.
            // (1) ensure that the array is defined and large enough
            if (laneAllowedVehicleTypesArray[segmentId] == null) {
                laneAllowedVehicleTypesArray[segmentId] = new ExtVehicleType?[segmentInfo.m_lanes.Length];
            } else if (laneAllowedVehicleTypesArray[segmentId].Length <
                       segmentInfo.m_lanes.Length) {
                ExtVehicleType?[] oldArray = laneAllowedVehicleTypesArray[segmentId];
                laneAllowedVehicleTypesArray[segmentId] = new ExtVehicleType?[segmentInfo.m_lanes.Length];
                Array.Copy(oldArray, laneAllowedVehicleTypesArray[segmentId], oldArray.Length);
            }

            // (2) insert the custom speed limit
            laneAllowedVehicleTypesArray[segmentId][laneIndex] = vehicleTypes;
        }

        public static void ResetSegmentVehicleRestrictions(ushort segmentId) {
            if (segmentId <= 0) {
                return;
            }
#if DEBUGFLAGS
            Log._Debug("Flags.resetSegmentVehicleRestrictions: Resetting vehicle restrictions " +
                       $"of segment {segmentId}.");
#endif
            laneAllowedVehicleTypesArray[segmentId] = null;
        }

        internal static IDictionary<uint, ExtVehicleType> GetAllLaneAllowedVehicleTypes() {
            IDictionary<uint, ExtVehicleType> ret = new Dictionary<uint, ExtVehicleType>();
            ExtSegmentManager extSegmentManager = ExtSegmentManager.Instance;

            for (ushort segmentId = 0; segmentId < NetManager.MAX_SEGMENT_COUNT; ++segmentId) {
                ref NetSegment segment = ref segmentId.ToSegment();
                if ((segment.m_flags & (NetSegment.Flags.Created | NetSegment.Flags.Deleted)) != NetSegment.Flags.Created) {
                    continue;
                }

                ExtVehicleType?[] allowedTypes = laneAllowedVehicleTypesArray[segmentId];
                if (allowedTypes == null) {
                    continue;
                }

                foreach (LaneIdAndIndex laneIdAndIndex in extSegmentManager.GetSegmentLaneIdsAndLaneIndexes(segmentId)) {
                    NetInfo.Lane laneInfo = segment.Info.m_lanes[laneIdAndIndex.laneIndex];

                    if (laneInfo.m_vehicleType == VehicleInfo.VehicleType.None) {
                        continue;
                    }

                    if (laneIdAndIndex.laneIndex >= allowedTypes.Length) {
                        continue;
                    }

                    ExtVehicleType? allowedType = allowedTypes[laneIdAndIndex.laneIndex];

                    if (allowedType == null) {
                        continue;
                    }

                    ret.Add(laneIdAndIndex.laneId, (ExtVehicleType)allowedType);
                }
            }

            return ret;
        }

        public static void ApplyAllFlags() {
        }

        internal static void OnLevelUnloading() {
        }

        public static void OnBeforeLoadData() { }
    }
}