namespace TrafficManager.Manager.Impl {
    using ColossalFramework;
    using CSUtil.Commons;
    using System.Collections.Generic;
    using System.Linq;
    using System;
    using TrafficManager.API.Manager;
    using TrafficManager.API.Traffic.Data;
    using TrafficManager.API.Traffic.Enums;
    using TrafficManager.State;
    using TrafficManager.Util;
    using UnityEngine;
    using static TrafficManager.Util.Shortcuts;
    using TrafficManager.Util.Extensions;
    using TrafficManager.Lifecycle;

    using StateFlags = TrafficManager.State.Flags;

    public class LaneArrowManager
        : AbstractGeometryObservingManager,
          ICustomDataManager<List<Configuration.LaneArrowData>>,
          ICustomDataManager<string>, ILaneArrowManager
        {
        /// <summary>
        /// lane types for all road vehicles.
        /// </summary>
        public const NetInfo.LaneType LANE_TYPES =
            NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle;

        /// <summary>
        /// vehcile types for all road vehicles
        /// </summary>
        public const VehicleInfo.VehicleType VEHICLE_TYPES = VehicleInfo.VehicleType.Car;

        public NetInfo.LaneType LaneTypes => LANE_TYPES;

        public VehicleInfo.VehicleType VehicleTypes => VEHICLE_TYPES;


        public const ExtVehicleType EXT_VEHICLE_TYPES =
            ExtVehicleType.RoadVehicle & ~ExtVehicleType.Emergency;

        public static readonly LaneArrowManager Instance = new LaneArrowManager();

        protected override void InternalPrintDebugInfo() {
            base.InternalPrintDebugInfo();
            Log.NotImpl("InternalPrintDebugInfo for LaneArrowManager");
        }

        public LaneArrows GetFinalLaneArrows(uint laneId) {
            return Flags.GetFinalLaneArrowFlags(laneId, true);
        }

        /// <summary>
        /// Set the lane arrows to flags. this will remove all default arrows for the lane
        /// and replace it with user arrows.
        /// default arrows may change as user connects or remove more segments to the junction but
        /// the user arrows stay the same no matter what.
        /// </summary>
        public bool SetLaneArrows(uint laneId,
                                  LaneArrows flags,
                                  bool overrideHighwayArrows = false) {
            if (Flags.SetLaneArrowFlags(laneId, flags, overrideHighwayArrows)) {
                OnLaneChange(laneId);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Add arrows to the lane. This will not remove any prevously set flags and
        /// will remove and replace default arrows only where flag is set.
        /// default arrows may change as user connects or remove more segments to the junction but
        /// the user arrows stay the same no matter what.
        /// </summary>
        /// <param name="laneId"></param>
        /// <param name="flags"></param>
        /// <param name="overrideHighwayArrows"></param>
        /// <returns></returns>
        public bool AddLaneArrows(uint laneId,
                                  LaneArrows flags,
                                  bool overrideHighwayArrows = false) {

            LaneArrows flags2 = GetFinalLaneArrows(laneId);
            return SetLaneArrows(laneId, flags | flags2, overrideHighwayArrows);
        }

        /// <summary>
        /// remove arrows (user or default) where flag is set.
        /// default arrows may change as user connects or remove more segments to the junction but
        /// the user arrows stay the same no matter what.
        /// </summary>
        public bool RemoveLaneArrows(uint laneId,
                          LaneArrows flags,
                          bool overrideHighwayArrows = false) {
            LaneArrows flags2 = GetFinalLaneArrows(laneId);
            return SetLaneArrows(laneId, ~flags & flags2, overrideHighwayArrows);
        }

        /// <summary>
        /// Toggles a lane arrows (user or default) on and off for the directions where flag is set.
        /// overrides default settings for the arrows that change.
        /// default arrows may change as user connects or remove more segments to the junction but
        /// the user arrows stay the same no matter what.
        /// </summary>
        public bool ToggleLaneArrows(uint laneId,
                                     bool startNode,
                                     LaneArrows flags,
                                     out SetLaneArrow_Result res) {
            if (Flags.ToggleLaneArrowFlags(laneId, startNode, flags, out res)) {
                OnLaneChange(laneId);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Resets lane arrows to their default value for the given segment end.
        /// </summary>
        /// <param name="segmentId">Segment id.</param>
        /// <param name="startNode">Determines the segment end to reset, or both if <c>null</c>.</param>
        public void ResetLaneArrows(ushort segmentId, bool? startNode = null) {

            ref NetSegment segment = ref segmentId.ToSegment();

            var sortedLanes = segment.GetSortedLanes(startNode, LANE_TYPES, VEHICLE_TYPES, sort: false);

            foreach (var lane in sortedLanes)
                ResetLaneArrows(lane.laneId);
        }

        /// <summary>
        /// Resets lane arrows to their default value for the given lane
        /// </summary>
        public void ResetLaneArrows(uint laneId) {
            if (Flags.ResetLaneArrowFlags(laneId)) {
                RecalculateFlags(laneId);
                OnLaneChange(laneId);
            }
        }

        /// <summary>
        /// Updates all road relevant segments so that the dedicated turning lane policy would take effect.
        /// </summary>
        /// <param name="recalculateRoutings">
        /// also recalculate lane transitions in routing manager.
        /// Current car paths will not be recalculated (to save time). New paths will follow the new lane routings.
        /// </param>
        public void UpdateDedicatedTurningLanePolicy(bool recalculateRoutings) {
            Log.Info($"UpdateDedicatedTurningLanePolicy(recalculateRoutings:{recalculateRoutings}) was called.");
            SimulationManager.instance.AddAction(delegate () {
                try {
                    Log._Debug($"Executing UpdateDedicatedTurningLanePolicy() in simulation thread ...");
                    for (ushort segmentId = 1; segmentId < NetManager.MAX_SEGMENT_COUNT; ++segmentId) {
                        ref NetSegment netSegment = ref segmentId.ToSegment();

                        if (!netSegment.IsValid())
                            continue;

                        if (netSegment.Info?.GetAI() is not RoadBaseAI ai)
                            continue;

                        int forward = 0, backward = 0;
                        segmentId.ToSegment().CountLanes(segmentId, LANE_TYPES, VEHICLE_TYPES, ref forward, ref backward);
                        if (forward == 1 && backward == 1) {
                            // one lane cannot have dedicated turning lanes.
                            continue;
                        }

                        if (netSegment.m_startNode.ToNode().CountSegments() <= 2 &&
                            netSegment.m_endNode.ToNode().CountSegments() <= 2) {
                            // no intersection.
                            continue;
                        }

                        ai.UpdateLanes(segmentId, ref netSegment, true);
                        NetManager.instance.UpdateSegmentRenderer(segmentId, true);
                        if (recalculateRoutings) {
                            RoutingManager.Instance.RequestRecalculation(segmentId, false);
                        }
                    }
                } catch(Exception ex) {
                    ex.LogException();
                }
            });
        }

        private static void RecalculateFlags(uint laneId) {
            ushort segmentId = laneId.ToLane().m_segment;
            ref NetSegment segment = ref segmentId.ToSegment();
            NetAI ai = segment.Info.m_netAI;
#if DEBUGFLAGS
            Log._Debug($"Flags.RecalculateFlags: Recalculateing lane arrows of segment {segmentId}.");
#endif
            ai.UpdateLanes(segmentId, ref segment, true);
        }

        private void OnLaneChange(uint laneId) {
            ushort segment = laneId.ToLane().m_segment;
            RoutingManager.Instance.RequestRecalculation(segment);
            if (TMPELifecycle.Instance.MayPublishSegmentChanges()) {
                ExtSegmentManager.Instance.PublishSegmentChanges(segment);
            }
        }

        protected override void HandleInvalidSegment(ref ExtSegment seg) {
            Flags.ResetSegmentArrowFlags(seg.segmentId);
        }

        protected override void HandleValidSegment(ref ExtSegment seg) { }

        private void ApplyFlags() {
            for (uint laneId = 0; laneId < NetManager.MAX_LANE_COUNT; ++laneId) {
                Flags.ApplyLaneArrowFlags(laneId);
            }
        }

        public override void OnLevelLoading() {
            base.OnLevelLoading();
            if (Options.DedicatedTurningLanes) {
                // update dedicated turning lanes after patch has been applied.
                UpdateDedicatedTurningLanePolicy(false);
            }
        }

        public override void OnBeforeSaveData() {
            base.OnBeforeSaveData();
            ApplyFlags();
        }

        public override void OnAfterLoadData() {
            base.OnAfterLoadData();
            Flags.ClearHighwayLaneArrows();
            ApplyFlags();
        }

        [Obsolete]
        public bool LoadData(string data) {
            bool success = true;
            Log.Info($"Loading lane arrow data (old method)");
#if DEBUGLOAD
            Log._Debug($"LaneFlags: {data}");
#endif
            var lanes = data.Split(',');

            if (lanes.Length <= 1) {
                return success;
            }

            foreach (string[] split in lanes.Select(lane => lane.Split(':'))
                                            .Where(split => split.Length > 1)) {
                try {
#if DEBUGLOAD
                    Log._Debug($"Split Data: {split[0]} , {split[1]}");
#endif
                    var laneId = Convert.ToUInt32(split[0]);
                    ref NetLane netLane = ref laneId.ToLane();

                    uint flags = Convert.ToUInt32(split[1]);

                    if (!netLane.IsValidWithSegment())
                        continue;

                    if (flags > ushort.MaxValue)
                        continue;

                    uint laneArrowFlags = flags & Flags.lfr;
#if DEBUGLOAD
                    uint origFlags = (laneId.ToLane().m_flags & Flags.lfr);

                    Log._Debug("Setting flags for lane " + laneId + " to " + flags + " (" +
                        ((Flags.LaneArrows)(laneArrowFlags)).ToString() + ")");
                    if ((origFlags | laneArrowFlags) == origFlags) {
                        // only load if setting differs from default
                        Log._Debug("Flags for lane " + laneId + " are original (" +
                            ((NetLane.Flags)(origFlags)).ToString() + ")");
                    }
#endif
                    SetLaneArrows(laneId, (LaneArrows)laneArrowFlags);
                }
                catch (Exception e) {
                    Log.Error(
                        $"Error loading Lane Split data. Length: {split.Length} value: {split}\n" +
                        $"Error: {e}");
                    success = false;
                }
            }

            return success;
        }

        [Obsolete]
        string ICustomDataManager<string>.SaveData(ref bool success) {
            return null;
        }

        public bool LoadData(List<Configuration.LaneArrowData> data) {
            bool success = true;
            Log.Info($"Loading lane arrow data (new method)");

            foreach (Configuration.LaneArrowData laneArrowData in data) {
                try {
                    ref NetLane netLane = ref laneArrowData.laneId.ToLane();

                    if (!netLane.IsValidWithSegment()) {
                        continue;
                    }

                    uint laneArrowFlags = laneArrowData.arrows & Flags.lfr;
                    SetLaneArrows(laneArrowData.laneId, (LaneArrows)laneArrowFlags);
                }
                catch (Exception e) {
                    Log.Error(
                        $"Error loading lane arrow data for lane {laneArrowData.laneId}, " +
                        $"arrows={laneArrowData.arrows}: {e}");
                    success = false;
                }
            }

            return success;
        }

        public List<Configuration.LaneArrowData> SaveData(ref bool success) {
            var ret = new List<Configuration.LaneArrowData>();

            for (uint i = 0; i < Singleton<NetManager>.instance.m_lanes.m_buffer.Length; i++) {
                try {
                    LaneArrows? laneArrows = Flags.GetLaneArrowFlags(i);

                    if (laneArrows == null) {
                        continue;
                    }

                    uint laneArrowInt = (uint)laneArrows;
#if DEBUGSAVE
                    Log._Debug($"Saving lane arrows for lane {i}, setting to {laneArrows} ({laneArrowInt})");
#endif
                    ret.Add(new Configuration.LaneArrowData(i, laneArrowInt));
                }
                catch (Exception e) {
                    Log.Error($"Exception occurred while saving lane arrows @ {i}: {e}");
                    success = false;
                }
            }

            return ret;
        }

        /// <summary>
        /// Used for loading and saving LaneFlags
        /// </summary>
        /// <returns>ICustomDataManager for lane flags as a string</returns>
        public static ICustomDataManager<string> AsLaneFlagsDM() {
            return Instance;
        }

        /// <summary>
        /// Used for loading and saving lane arrows
        /// </summary>
        /// <returns>ICustomDataManager for lane arrows</returns>
        public static ICustomDataManager<List<Configuration.LaneArrowData>> AsLaneArrowsDM() {
            return Instance;
        }

        internal LaneArrows? GetLaneArrowFlags(uint laneId) => Flags.GetLaneArrowFlags(laneId);

        internal bool ApplyLaneArrowFlags(uint laneId, bool check = true) => Flags.ApplyLaneArrowFlags(laneId, check);

        internal void RemoveLaneArrowFlags(uint laneId) => Flags.RemoveLaneArrowFlags(laneId);

        internal bool CanHaveLaneArrows(uint laneId, bool? startNode = null) => Flags.CanHaveLaneArrows(laneId, startNode);

        internal void ClearHighwayLaneArrows() => Flags.ClearHighwayLaneArrows();

        internal void RemoveHighwayLaneArrowFlags(uint laneId) => Flags.RemoveHighwayLaneArrowFlags(laneId);

        internal LaneArrows? GetHighwayLaneArrowFlags(uint laneId) => Flags.GetHighwayLaneArrowFlags(laneId);

        internal void SetHighwayLaneArrowFlags(uint laneId, LaneArrows flags, bool check = true)
            => Flags.SetHighwayLaneArrowFlags(laneId, flags, check);

        internal void RemoveHighwayLaneArrowFlagsAtSegment(ushort segmentId) => Flags.RemoveHighwayLaneArrowFlagsAtSegment(segmentId);

        private static class Flags {

            public static readonly uint lfr = (uint)NetLane.Flags.LeftForwardRight;


            /// <summary>
            /// For each lane: Defines the lane arrows which are set
            /// </summary>
            private static LaneArrows?[] laneArrowFlags;

            /// <summary>
            /// For each lane: Defines the lane arrows which are set in highway rule mode (they are not saved)
            /// </summary>
            private static LaneArrows?[] highwayLaneArrowFlags;

            static Flags() {
                laneArrowFlags = new LaneArrows?[NetManager.MAX_LANE_COUNT];
                highwayLaneArrowFlags = new LaneArrows?[NetManager.MAX_LANE_COUNT];
            }

            /// <summary>Called from Debug Panel.</summary>
            internal static void PrintDebugInfo() {
                Log.Info("------------------------");
                Log.Info("--- LANE ARROW FLAGS ---");
                Log.Info("------------------------");
                for (uint i = 0; i < laneArrowFlags.Length; ++i) {
                    ref NetLane netLane = ref i.ToLane();

                    if (highwayLaneArrowFlags[i] != null || laneArrowFlags[i] != null) {
                        Log.Info($"Lane {i}: valid? {netLane.IsValidWithSegment()}");
                    }

                    if (highwayLaneArrowFlags[i] != null) {
                        Log.Info($"\thighway arrows: {highwayLaneArrowFlags[i]}");
                    }

                    if (laneArrowFlags[i] != null) {
                        Log.Info($"\tcustom arrows: {laneArrowFlags[i]}");
                    }
                }
            }

            public static void ResetSegmentArrowFlags(ushort segmentId) {
                if (segmentId <= 0) {
                    return;
                }

                ref NetSegment netSegment = ref segmentId.ToSegment();
#if DEBUGFLAGS
            Log._Debug($"Flags.resetSegmentArrowFlags: Resetting lane arrows of segment {segmentId}.");
#endif
                NetManager netManager = Singleton<NetManager>.instance;
                NetInfo segmentInfo = netSegment.Info;
                uint curLaneId = netSegment.m_lanes;
                int numLanes = segmentInfo.m_lanes.Length;
                int laneIndex = 0;

                while (laneIndex < numLanes && curLaneId != 0u) {
#if DEBUGFLAGS
                Log._Debug($"Flags.resetSegmentArrowFlags: Resetting lane arrows of segment {segmentId}: " +
                           $"Resetting lane {curLaneId}.");
#endif
                    laneArrowFlags[curLaneId] = null;

                    curLaneId = curLaneId.ToLane().m_nextLane;
                    ++laneIndex;
                }
            }

            /// <summary>
            /// removes the custom lane arrow flags. requires post recalculation.
            /// </summary>
            /// <param name="laneId"></param>
            /// <returns><c>true</c>on success, <c>false</c> otherwise</returns>
            public static bool ResetLaneArrowFlags(uint laneId) {
#if DEBUGFLAGS
            Log._Debug($"Flags.resetLaneArrowFlags: Resetting lane arrows of lane {laneId}.");
#endif
                if (LaneConnection.LaneConnectionManager.Instance.Sub.HasOutgoingConnections(laneId)) {
                    return false;
                }

                laneArrowFlags[laneId] = null;
                return true;
            }

            public static bool SetLaneArrowFlags(uint laneId,
                                                 LaneArrows flags,
                                                 bool overrideHighwayArrows = false) {
#if DEBUGFLAGS
            Log._Debug($"Flags.setLaneArrowFlags({laneId}, {flags}, {overrideHighwayArrows}) called");
#endif

                if (!CanHaveLaneArrows(laneId)) {
#if DEBUGFLAGS
                Log._Debug($"Flags.setLaneArrowFlags({laneId}, {flags}, {overrideHighwayArrows}): " +
                           $"lane must not have lane arrows");
#endif
                    RemoveLaneArrowFlags(laneId);
                    return false;
                }

                if (!overrideHighwayArrows && highwayLaneArrowFlags[laneId] != null) {
#if DEBUGFLAGS
                Log._Debug($"Flags.setLaneArrowFlags({laneId}, {flags}, {overrideHighwayArrows}): " +
                           "highway arrows may not be overridden");
#endif
                    return false; // disallow custom lane arrows in highway rule mode
                }

                if (overrideHighwayArrows) {
#if DEBUGFLAGS
                Log._Debug($"Flags.setLaneArrowFlags({laneId}, {flags}, {overrideHighwayArrows}): " +
                           $"overriding highway arrows");
#endif
                    highwayLaneArrowFlags[laneId] = null;
                }

#if DEBUGFLAGS
            Log._Debug($"Flags.setLaneArrowFlags({laneId}, {flags}, {overrideHighwayArrows}): setting flags");
#endif
                laneArrowFlags[laneId] = flags;
                return ApplyLaneArrowFlags(laneId, false);
            }

            public static void SetHighwayLaneArrowFlags(uint laneId,
                                                        LaneArrows flags,
                                                        bool check = true) {
                if (check && !CanHaveLaneArrows(laneId)) {
                    RemoveLaneArrowFlags(laneId);
                    return;
                }

                highwayLaneArrowFlags[laneId] = flags;
#if DEBUGFLAGS
            Log._Debug($"Flags.setHighwayLaneArrowFlags: Setting highway arrows of lane {laneId} to {flags}");
#endif
                ApplyLaneArrowFlags(laneId, false);
            }

            public static bool ToggleLaneArrowFlags(uint laneId,
                                                    bool startNode,
                                                    LaneArrows flags,
                                                    out SetLaneArrow_Result res) {
                if (!CanHaveLaneArrows(laneId)) {
                    RemoveLaneArrowFlags(laneId);
                    res = SetLaneArrow_Result.Invalid;
                    return false;
                }

                if (highwayLaneArrowFlags[laneId] != null) {
                    res = SetLaneArrow_Result.HighwayArrows;
                    return false; // disallow custom lane arrows in highway rule mode
                }

                if (LaneConnection.LaneConnectionManager.Instance.Sub.HasOutgoingConnections(laneId, startNode)) {
                    // TODO refactor
                    res = SetLaneArrow_Result.LaneConnection;
                    return false; // custom lane connection present
                }

                ref NetLane netLane = ref laneId.ToLane();

                LaneArrows? arrows = laneArrowFlags[laneId];
                if (arrows == null) {
                    // read currently defined arrows
                    uint laneFlags = netLane.m_flags;
                    laneFlags &= lfr; // filter arrows
                    arrows = (LaneArrows)laneFlags;
                }

                arrows ^= flags;
                laneArrowFlags[laneId] = arrows;
                if (ApplyLaneArrowFlags(laneId, false)) {
                    res = SetLaneArrow_Result.Success;
                    return true;
                }

                res = SetLaneArrow_Result.Invalid;
                return false;
            }

            internal static bool CanHaveLaneArrows(uint laneId, bool? startNode = null) {
                if (laneId <= 0) {
                    return false;
                }

                ref NetLane netLane = ref laneId.ToLane();

                if (((NetLane.Flags)netLane.m_flags & (NetLane.Flags.Created | NetLane.Flags.Deleted)) != NetLane.Flags.Created) {
                    return false;
                }

                ref NetSegment netSegment = ref netLane.m_segment.ToSegment();

                const NetInfo.Direction dir = NetInfo.Direction.Forward;
                NetInfo.Direction dir2 = ((netSegment.m_flags & NetSegment.Flags.Invert) == NetSegment.Flags.None)
                    ? dir
                    : NetInfo.InvertDirection(dir);

                NetInfo segmentInfo = netSegment.Info;
                uint curLaneId = netSegment.m_lanes;
                int numLanes = segmentInfo.m_lanes.Length;
                int laneIndex = 0;
                int wIter = 0;

                while (laneIndex < numLanes && curLaneId != 0u) {
                    ++wIter;
                    if (wIter >= 100) {
                        Log.Error("Too many iterations in Flags.mayHaveLaneArrows!");
                        break;
                    }

                    if (curLaneId == laneId) {
                        NetInfo.Lane laneInfo = segmentInfo.m_lanes[laneIndex];
                        bool isStartNode = (laneInfo.m_finalDirection & dir2) == NetInfo.Direction.None;
                        if (startNode != null && isStartNode != startNode) {
                            return false;
                        }

                        ushort nodeId = isStartNode
                            ? netSegment.m_startNode
                            : netSegment.m_endNode;
                        ref NetNode netNode = ref nodeId.ToNode();

                        return (netNode.m_flags & (NetNode.Flags.Created | NetNode.Flags.Deleted)) == NetNode.Flags.Created
                            && (netNode.m_flags & NetNode.Flags.Junction) != NetNode.Flags.None;
                    }

                    curLaneId = curLaneId.ToLane().m_nextLane;
                    ++laneIndex;
                }

                return false;
            }

            public static LaneArrows? GetLaneArrowFlags(uint laneId) {
                return laneArrowFlags[laneId];
            }

            public static LaneArrows? GetHighwayLaneArrowFlags(uint laneId) {
                return highwayLaneArrowFlags[laneId];
            }

            public static void RemoveHighwayLaneArrowFlags(uint laneId) {
#if DEBUGFLAGS
            Log._Debug(
                $"Flags.removeHighwayLaneArrowFlags: Removing highway arrows of lane {laneId}");
#endif
                if (highwayLaneArrowFlags[laneId] != null) {
                    highwayLaneArrowFlags[laneId] = null;
                    ApplyLaneArrowFlags(laneId, false);
                }
            }

            public static void ApplyAllFlags() {
                for (uint i = 0; i < laneArrowFlags.Length; ++i) {
                    ApplyLaneArrowFlags(i);
                }
            }

            public static bool ApplyLaneArrowFlags(uint laneId, bool check = true) {
#if DEBUGFLAGS
            Log._Debug($"Flags.applyLaneArrowFlags({laneId}, {check}) called");
#endif

                if (laneId <= 0) {
                    return true;
                }

                if (check && !CanHaveLaneArrows(laneId)) {
                    RemoveLaneArrowFlags(laneId);
                    return false;
                }

                LaneArrows? hwArrows = highwayLaneArrowFlags[laneId];
                LaneArrows? arrows = laneArrowFlags[laneId];
                uint laneFlags = laneId.ToLane().m_flags;

                if (hwArrows != null) {
                    laneFlags &= ~lfr; // remove all arrows
                    laneFlags |= (uint)hwArrows; // add highway arrows
                } else if (arrows != null) {
                    LaneArrows flags = (LaneArrows)arrows;
                    laneFlags &= ~lfr; // remove all arrows
                    laneFlags |= (uint)flags; // add desired arrows
                }

#if DEBUGFLAGS
            Log._Debug($"Flags.applyLaneArrowFlags: Setting lane flags of lane {laneId} to " +
                       $"{(NetLane.Flags)laneFlags}");
#endif
                laneId.ToLane().m_flags = Convert.ToUInt16(laneFlags);
                return true;
            }

            public static LaneArrows GetFinalLaneArrowFlags(uint laneId, bool check = true) {
                if (!CanHaveLaneArrows(laneId)) {
#if DEBUGFLAGS
                Log._Debug($"Lane {laneId} may not have lane arrows");
#endif
                    return LaneArrows.None;
                }

                uint ret = 0;
                LaneArrows? hwArrows = highwayLaneArrowFlags[laneId];
                LaneArrows? arrows = laneArrowFlags[laneId];

                if (hwArrows != null) {
                    ret &= ~lfr; // remove all arrows
                    ret |= (uint)hwArrows; // add highway arrows
                } else if (arrows != null) {
                    LaneArrows flags = (LaneArrows)arrows;
                    ret &= ~lfr; // remove all arrows
                    ret |= (uint)flags; // add desired arrows
                } else {
                    ret = laneId.ToLane().m_flags;
                    ret &= (uint)LaneArrows.LeftForwardRight;
                }

                return (LaneArrows)ret;
            }

            public static void RemoveLaneArrowFlags(uint laneId) {
                if (laneId <= 0) {
                    return;
                }

                ref NetLane netLane = ref laneId.ToLane();

                if (highwayLaneArrowFlags[laneId] != null) {
                    return; // modification of arrows in highway rule mode is forbidden
                }

                laneArrowFlags[laneId] = null;

                // uint laneFlags = netLane.m_flags;
                if (((NetLane.Flags)netLane.m_flags & (NetLane.Flags.Created | NetLane.Flags.Deleted)) == NetLane.Flags.Created) {
                    netLane.m_flags &= (ushort)~lfr;
                }
            }

            internal static void RemoveHighwayLaneArrowFlagsAtSegment(ushort segmentId) {
                ref NetSegment netSegment = ref segmentId.ToSegment();

                if ((netSegment.m_flags & (NetSegment.Flags.Created | NetSegment.Flags.Deleted)) != NetSegment.Flags.Created) {
                    return;
                }

                int i = 0;
                uint curLaneId = netSegment.m_lanes;

                int segmentLanesCount = netSegment.Info.m_lanes.Length;
                while (i < segmentLanesCount && curLaneId != 0u) {
                    RemoveHighwayLaneArrowFlags(curLaneId);
                    curLaneId = curLaneId.ToLane().m_nextLane;
                    ++i;
                } // foreach lane
            }

            public static void ClearHighwayLaneArrows() {
                uint lanesCount = Singleton<NetManager>.instance.m_lanes.m_size;
                for (uint i = 0; i < lanesCount; ++i) {
                    highwayLaneArrowFlags[i] = null;
                }
            }

            internal static void OnLevelUnloading() {
                for (uint i = 0; i < StateFlags.laneAllowedVehicleTypesArray.Length; ++i) {
                    StateFlags.laneAllowedVehicleTypesArray[i] = null;
                }

                for (uint i = 0; i < laneArrowFlags.Length; ++i) {
                    laneArrowFlags[i] = null;
                }

                for (uint i = 0; i < highwayLaneArrowFlags.Length; ++i) {
                    highwayLaneArrowFlags[i] = null;
                }
            }
        }
    }
}