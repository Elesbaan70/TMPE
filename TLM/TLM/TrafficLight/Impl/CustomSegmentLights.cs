// #define DEBUGGET

namespace TrafficManager.TrafficLight.Impl {
    using ColossalFramework;
    using CSUtil.Commons;
    using JetBrains.Annotations;
    using System.Collections.Generic;
    using System;
    using TrafficManager.API.Manager;
    using TrafficManager.API.Traffic.Data;
    using TrafficManager.API.Traffic.Enums;
    using TrafficManager.Geometry.Impl;
    using TrafficManager.State.ConfigData;
    using TrafficManager.Util;
    using TrafficManager.Manager.Impl;
    using TrafficManager.Util.Extensions;
    using System.Linq;

    /// <summary>
    /// Represents the set of custom traffic lights located at a node
    /// </summary>
    internal class CustomSegmentLights
        : SegmentEndId {
        // private static readonly ExtVehicleType[] SINGLE_LANE_VEHICLETYPES
        // = new ExtVehicleType[] { ExtVehicleType.Tram, ExtVehicleType.Service,
        // ExtVehicleType.CargoTruck, ExtVehicleType.RoadPublicTransport
        // | ExtVehicleType.Service, ExtVehicleType.RailVehicle };
        public const ExtVehicleType DEFAULT_MAIN_VEHICLETYPE = ExtVehicleType.None;
        internal static readonly SegmentLightGroup defaultGroup = new SegmentLightGroup(DEFAULT_MAIN_VEHICLETYPE);

        [Obsolete]
        protected CustomSegmentLights(ITrafficLightContainer lightsContainer,
                                      ushort nodeId,
                                      ushort segmentId,
                                      bool calculateAutoPedLight)
            : this(
                lightsContainer,
                segmentId,
                nodeId == segmentId.ToSegment().m_startNode,
                calculateAutoPedLight) { }

        public CustomSegmentLights(ITrafficLightContainer lightsContainer,
                                   ushort segmentId,
                                   bool startNode,
                                   bool calculateAutoPedLight)
            : this(
                lightsContainer,
                segmentId,
                startNode,
                calculateAutoPedLight,
                true) { }

        public CustomSegmentLights(ITrafficLightContainer lightsContainer,
                                   ushort segmentId,
                                   bool startNode,
                                   bool calculateAutoPedLight,
                                   bool performHousekeeping)
            : base(segmentId, startNode) {
            this.lightsContainer = lightsContainer;
            if (performHousekeeping) {
                Housekeeping(false, calculateAutoPedLight);
            }
        }

        [Obsolete]
        public ushort NodeId => StartNode
            ? SegmentId.ToSegment().m_startNode
            : SegmentId.ToSegment().m_endNode;

        private uint LastChangeFrame;

        // TODO improve & remove
        public bool InvalidPedestrianLight { get; set; } = false;

        public IDictionary<SegmentLightGroup, CustomSegmentLight> CustomLights {
            get;
        } = new Dictionary<SegmentLightGroup, CustomSegmentLight>();

        // TODO replace collection
        public LinkedList<SegmentLightGroup> Groups {
            get; private set;
        } = new LinkedList<SegmentLightGroup>();

        public SegmentLightGroup?[] GroupsByLaneIndex {
            get; private set;
        } = new SegmentLightGroup?[0];

        /// <summary>
        /// Vehicles types that have their own traffic light
        /// </summary>
        private ExtVehicleType SeparateVehicleTypes {
            get;
            set;
        } = ExtVehicleType.None;

        // TODO set should be private
        public RoadBaseAI.TrafficLightState AutoPedestrianLightState { get; set; } =
            RoadBaseAI.TrafficLightState.Green;

        public RoadBaseAI.TrafficLightState? PedestrianLightState {
            get {
                if (InvalidPedestrianLight || InternalPedestrianLightState == null) {
                    // no pedestrian crossing at this point
                    return RoadBaseAI.TrafficLightState.Green;
                }

                return ManualPedestrianMode && InternalPedestrianLightState != null
                           ? (RoadBaseAI.TrafficLightState)InternalPedestrianLightState
                           : AutoPedestrianLightState;
            }

            set {
                if (InternalPedestrianLightState == null) {
#if DEBUGHK
                    Log._Debug($"CustomSegmentLights: Refusing to change pedestrian light at segment {SegmentId}");
#endif
                    return;
                }

                // Log._Debug($"CustomSegmentLights: Setting pedestrian light at segment {segmentId}");
                InternalPedestrianLightState = value;
            }
        }

        public bool ManualPedestrianMode {
            get => manualPedestrianMode;
            set {
                if (!manualPedestrianMode && value) {
                    PedestrianLightState = AutoPedestrianLightState;
                }

                manualPedestrianMode = value;
            }
        }

        private bool manualPedestrianMode;

        public RoadBaseAI.TrafficLightState? InternalPedestrianLightState { get; private set; }

        private ExtVehicleType mainVehicleType = ExtVehicleType.None;

        protected CustomSegmentLight MainSegmentLight {
            get {
                CustomLights.TryGetValue(new SegmentLightGroup(mainVehicleType), out CustomSegmentLight res);
                return res;
            }
        }

        public ITrafficLightContainer LightsContainer {
            get => lightsContainer;

            [UsedImplicitly]
            set {
                lightsContainer = value;
                OnChange();
            }
        }

        private ITrafficLightContainer lightsContainer;

        public override string ToString() {
            return string.Format(
                "[CustomSegmentLights {0} @ node {1}\n\tLastChangeFrame: {2}\n\tInvalidPedestrianLight: " +
                "{3}\n\tCustomLights: {4}\n\tVehicleTypes: {5}\n\tVehicleTypeByLaneIndex: {6}\n" +
                "\tSeparateVehicleTypes: {7}\n\tAutoPedestrianLightState: {8}\n\tPedestrianLightState: " +
                "{9}\n\tManualPedestrianMode: {10}\n\tmanualPedestrianMode: {11}\n\t" +
                "InternalPedestrianLightState: {12}\n\tMainSegmentLight: {13}\nCustomSegmentLights]",
                base.ToString(),
                NodeId,
                LastChangeFrame,
                InvalidPedestrianLight,
                CustomLights,
                Groups.CollectionToString(),
                GroupsByLaneIndex.ArrayToString(),
                SeparateVehicleTypes,
                AutoPedestrianLightState,
                PedestrianLightState,
                ManualPedestrianMode,
                manualPedestrianMode,
                InternalPedestrianLightState,
                MainSegmentLight);
        }

        public bool Relocate(ushort segmentId,
                             bool startNode,
                             ITrafficLightContainer lightsContainer) {
            if (Relocate(segmentId, startNode)) {
                this.lightsContainer = lightsContainer;
                Housekeeping(true, true);
                return true;
            }

            return false;
        }

        public bool IsAnyGreen() {
            foreach (CustomSegmentLight v in CustomLights.Values) {
                if (v.IsAnyGreen()) {
                    return true;
                }
            }

            return false;
        }

        public bool IsAnyInTransition() {
            foreach (CustomSegmentLight v in CustomLights.Values) {
                if (v.IsAnyInTransition()) {
                    return true;
                }
            }

            return false;
        }

        public bool IsAnyLeftGreen() {
            foreach (CustomSegmentLight v in CustomLights.Values) {
                if (v.IsLeftGreen()) {
                    return true;
                }
            }

            return false;
        }

        public bool IsAnyMainGreen() {
            foreach (CustomSegmentLight v in CustomLights.Values) {
                if (v.IsMainGreen()) {
                    return true;
                }
            }

            return false;
        }

        public bool IsAnyRightGreen() {
            foreach (CustomSegmentLight v in CustomLights.Values) {
                if (v.IsRightGreen()) {
                    return true;
                }
            }

            return false;
        }

        public bool IsAllLeftRed() {
            foreach (CustomSegmentLight v in CustomLights.Values) {
                if (!v.IsLeftRed()) {
                    return false;
                }
            }

            return true;
        }

        public bool IsAllMainRed() {
            foreach (CustomSegmentLight v in CustomLights.Values) {
                if (!v.IsMainRed()) {
                    return false;
                }
            }

            return true;
        }

        public bool IsAllRightRed() {
            foreach (CustomSegmentLight v in CustomLights.Values) {
                if (!v.IsRightRed()) {
                    return false;
                }
            }

            return true;
        }

        public void UpdateVisuals() {
            MainSegmentLight?.UpdateVisuals();
        }

        public object Clone() {
            return Clone(LightsContainer, true);
        }

        public CustomSegmentLights Clone(ITrafficLightContainer newLightsManager,
                                          bool performHousekeeping = true) {
            var clone = new CustomSegmentLights(
                newLightsManager ?? LightsContainer,
                SegmentId,
                StartNode,
                false,
                false);

            foreach (KeyValuePair<SegmentLightGroup, CustomSegmentLight> e in CustomLights) {
                clone.CustomLights.Add(e.Key, (CustomSegmentLight)e.Value.Clone());
            }

            clone.InternalPedestrianLightState = InternalPedestrianLightState;
            clone.manualPedestrianMode = manualPedestrianMode;
            clone.Groups = new LinkedList<SegmentLightGroup>(Groups);
            clone.LastChangeFrame = LastChangeFrame;
            clone.mainVehicleType = mainVehicleType;
            clone.AutoPedestrianLightState = AutoPedestrianLightState;

            if (performHousekeeping) {
                clone.Housekeeping(false, false);
            }

            return clone;
        }

        public CustomSegmentLight GetCustomLight(byte laneIndex) {
            if (laneIndex >= GroupsByLaneIndex.Length) {
#if DEBUGGET
                Log._Debug($"CustomSegmentLights.GetCustomLight({laneIndex}): No vehicle type "+
                $"found for lane index");
#endif
                return MainSegmentLight;
            }

            SegmentLightGroup? group = GroupsByLaneIndex[laneIndex];

            if (group == null) {
#if DEBUGGET
                Log._Debug($"CustomSegmentLights.GetCustomLight({laneIndex}): No group found "+
                $"for lane index: lane is invalid");
#endif
                return MainSegmentLight;
            }

#if DEBUGGET
            Log._Debug($"CustomSegmentLights.GetCustomLight({laneIndex}): Group is {group}");
#endif

            if (!CustomLights.TryGetValue((SegmentLightGroup)group, out CustomSegmentLight light)
                    && !CustomLights.TryGetValue(group.Value.ForVehicleType(mainVehicleType), out light)) {
#if DEBUGGET
                Log._Debug($"CustomSegmentLights.GetCustomLight({laneIndex}): No custom light "+
                $"found for group {group}");
#endif
                return MainSegmentLight;
            }
#if DEBUGGET
            Log._Debug($"CustomSegmentLights.GetCustomLight({laneIndex}): Returning custom light "+
            $"for group {group}");
#endif
            return light;
        }

        public CustomSegmentLight GetCustomLight(SegmentLightGroup group) {
            if (!CustomLights.TryGetValue(group, out CustomSegmentLight ret)
                    && !CustomLights.TryGetValue(group.ForVehicleType(mainVehicleType), out ret)) {
                ret = MainSegmentLight;
            }

            return ret;

            // if (group != ExtVehicleType.None)
            //  Log._Debug($"No traffic light for vehicle type {group} defined at
            //     segment {segmentId}, node {nodeId}.");
        }

        public void MakeRed() {
            foreach (CustomSegmentLight v in CustomLights.Values) {
                v.MakeRed();
            }
        }

        public void MakeRedOrGreen() {
            foreach (CustomSegmentLight v in CustomLights.Values) {
                v.MakeRedOrGreen();
            }
        }

        public void SetLights(RoadBaseAI.TrafficLightState lightState) {
            foreach (CustomSegmentLight v in CustomLights.Values) {
                v.SetStates(lightState, lightState, lightState, false);
            }

            CalculateAutoPedestrianLightState(ref NodeId.ToNode());
        }

        public void SetLights(CustomSegmentLights otherLights) {
            foreach (KeyValuePair<SegmentLightGroup, CustomSegmentLight> e in otherLights.CustomLights) {
                if (!CustomLights.TryGetValue(e.Key, out CustomSegmentLight ourLight)) {
                    continue;
                }

                ourLight.SetStates(e.Value.LightMain, e.Value.LightLeft, e.Value.LightRight, false);
                // ourLight.LightPedestrian = e.Value.LightPedestrian;
            }

            InternalPedestrianLightState = otherLights.InternalPedestrianLightState;
            manualPedestrianMode = otherLights.ManualPedestrianMode;
            AutoPedestrianLightState = otherLights.AutoPedestrianLightState;
        }

        public void ChangeLightPedestrian() {
            if (PedestrianLightState == null) {
                return;
            }

            var invertedLight = PedestrianLightState == RoadBaseAI.TrafficLightState.Green
                                    ? RoadBaseAI.TrafficLightState.Red
                                    : RoadBaseAI.TrafficLightState.Green;

            PedestrianLightState = invertedLight;
            UpdateVisuals();
        }

        private static uint GetCurrentFrame() {
            return Singleton<SimulationManager>.instance.m_currentFrameIndex >> 6;
        }

        public uint LastChange() {
            return GetCurrentFrame() - LastChangeFrame;
        }

        public void OnChange(bool calculateAutoPedLight = true) {
            LastChangeFrame = GetCurrentFrame();

            if (calculateAutoPedLight) {
                CalculateAutoPedestrianLightState(ref NodeId.ToNode());
            }
        }

        public void CalculateAutoPedestrianLightState(ref NetNode node, bool propagate = true) {
#if DEBUG
            bool logTrafficLights = DebugSwitch.TimedTrafficLights.Get()
                                    && DebugSettings.NodeId == NodeId;
#else
            const bool logTrafficLights = false;
#endif

            if (logTrafficLights) {
                Log._Debug("CustomSegmentLights.CalculateAutoPedestrianLightState: Calculating " +
                           $"pedestrian light state of seg. {SegmentId} @ node {NodeId}");
            }

            IExtSegmentManager segMan = Constants.ManagerFactory.ExtSegmentManager;
            IExtSegmentEndManager segEndMan = Constants.ManagerFactory.ExtSegmentEndManager;
            ExtSegment seg = segMan.ExtSegments[SegmentId];
            ExtSegmentEnd segEnd = segEndMan.ExtSegmentEnds[segEndMan.GetIndex(SegmentId, StartNode)];
            ushort nodeId = segEnd.nodeId;

            if (nodeId != NodeId) {
                Log.Warning("CustomSegmentLights.CalculateAutoPedestrianLightState: Node id " +
                            $"mismatch! segment end node is {nodeId} but we are node {NodeId}. " +
                            $"segEnd={segEnd} this={this}");
                return;
            }

            if (propagate) {
                for (int i = 0; i < 8; ++i) {
                    ushort otherSegmentId = node.GetSegment(i);

                    if (otherSegmentId == 0 || otherSegmentId == SegmentId) {
                        continue;
                    }

                    CustomSegmentLights otherLights = LightsContainer.GetSegmentLights(nodeId, otherSegmentId);
                    if (otherLights == null) {
                        Log._DebugIf(
                            logTrafficLights,
                            () => "CustomSegmentLights.CalculateAutoPedestrianLightState: " +
                            $"Expected other (propagate) CustomSegmentLights at segment {otherSegmentId} " +
                            $"@ {NodeId} but there was none. Original segment id: {SegmentId}");

                        continue;
                    }

                    otherLights.CalculateAutoPedestrianLightState(ref node, false);
                }
            }

            if (IsAnyGreen()) {
                if (logTrafficLights) {
                    Log._Debug("CustomSegmentLights.CalculateAutoPedestrianLightState: Any green " +
                               $"at seg. {SegmentId} @ {NodeId}");
                }

                AutoPedestrianLightState = RoadBaseAI.TrafficLightState.Red;
                return;
            }

            Log._DebugIf(
                logTrafficLights,
                () => "CustomSegmentLights.CalculateAutoPedestrianLightState: Querying incoming " +
                $"segments at seg. {SegmentId} @ {NodeId}");

            ItemClass prevConnectionClass = SegmentId.ToSegment().Info.GetConnectionClass();
            var autoPedestrianLightState = RoadBaseAI.TrafficLightState.Green;
            ExtSegmentManager extSegmentManager = ExtSegmentManager.Instance;

            if (!(segEnd.incoming && seg.oneWay)) {
                for (int segmentIndex = 0; segmentIndex < Constants.MAX_SEGMENTS_OF_NODE; ++segmentIndex) {
                    ushort otherSegmentId = node.GetSegment(segmentIndex);

                    if (otherSegmentId == 0 || otherSegmentId == SegmentId) {
                        continue;
                    }

                    ref NetSegment otherSegment = ref otherSegmentId.ToSegment();

                    // ExtSegment otherSeg = segMan.ExtSegments[otherSegmentId];
                    int index0 = segEndMan.GetIndex(
                        otherSegmentId,
                        otherSegment.IsStartNode(NodeId));

                    if (!segEndMan.ExtSegmentEnds[index0].incoming) {
                        continue;
                    }

                    Log._DebugIf(
                        logTrafficLights,
                        () => "CustomSegmentLights.CalculateAutoPedestrianLightState: Checking " +
                        $"incoming straight segment {otherSegmentId} at seg. {SegmentId} @ {NodeId}");

                    CustomSegmentLights otherLights = LightsContainer.GetSegmentLights(nodeId, otherSegmentId);

                    if (otherLights == null) {
                        Log._DebugIf(
                            logTrafficLights,
                            () => "CustomSegmentLights.CalculateAutoPedestrianLightState: " +
                            $"Expected other (straight) CustomSegmentLights at segment {otherSegmentId} " +
                            $"@ {NodeId} but there was none. Original segment id: {SegmentId}");
                        continue;
                    }

                    ItemClass nextConnectionClass = otherSegment.Info.GetConnectionClass();
                    if (nextConnectionClass.m_service != prevConnectionClass.m_service) {
                        if (logTrafficLights) {
                            Log._DebugFormat(
                                "CustomSegmentLights.CalculateAutoPedestrianLightState: Other (straight) " +
                                "segment {0} @ {1} has different connection service than segment {2} " +
                                "({3} vs. {4}). Ignoring traffic light state.",
                                otherSegmentId,
                                NodeId,
                                SegmentId,
                                nextConnectionClass.m_service,
                                prevConnectionClass.m_service);
                        }

                        continue;
                    }

                    ArrowDirection dir = segEndMan.GetDirection(ref segEnd, otherSegmentId);
                    bool lht = Shortcuts.LHT;
                    if (dir == ArrowDirection.Forward) {
                        if (!otherLights.IsAllMainRed()) {
                            Log._DebugIf(
                                logTrafficLights,
                                () => "CustomSegmentLights.CalculateAutoPedestrianLightState: Not " +
                                $"all main red at {otherSegmentId} at seg. {SegmentId} @ {NodeId}");

                            autoPedestrianLightState = RoadBaseAI.TrafficLightState.Red;
                            break;
                        }
                    } else if (((dir == ArrowDirection.Left && lht)
                                || (dir == ArrowDirection.Right && !lht))
                               && ((lht && !otherLights.IsAllRightRed())
                                   || (!lht && !otherLights.IsAllLeftRed()))) {
                        Log._DebugIf(
                            logTrafficLights,
                            () => "CustomSegmentLights.CalculateAutoPedestrianLightState: " +
                            $"Not all left red at {otherSegmentId} at seg. {SegmentId} @ {NodeId}");

                        autoPedestrianLightState = RoadBaseAI.TrafficLightState.Red;
                        break;
                    }
                }
            }

            AutoPedestrianLightState = autoPedestrianLightState;

            Log._DebugIf(
                logTrafficLights,
                () => "CustomSegmentLights.CalculateAutoPedestrianLightState: Calculated " +
                $"AutoPedestrianLightState for segment {SegmentId} @ {NodeId}: {AutoPedestrianLightState}");
        }

        private class LaneData {
            public SegmentLightGroup group;
            public LaneArrows laneArrows;
            public NetInfo.Lane laneInfo;
            public ExtVehicleType defaultMask;

            public override string ToString() {
                return $"[LaneData\n" +
                        $"\tgroup={group}" +
                        $"\tlaneArrows={laneArrows}" +
                        $"\tlaneInfo={laneInfo}" +
                        $"\tdefaultMask={defaultMask}" +
                        "LaneData]";
            }
        }

        private Dictionary<byte, LaneData> GetAllLaneData() {
            var vehicleTypes = VehicleRestrictionsManager.Instance.GetAllowedVehicleTypesAsDict(SegmentId, NodeId, VehicleRestrictionsMode.Restricted);
            var segmentInfo = SegmentId.ToSegment().Info;

            bool startNode = NodeId == SegmentId.ToSegment().m_startNode;

            var segMgr = ExtSegmentManager.Instance;
            var endMgr = LaneEndManager.Instance;

            var result = new Dictionary<byte, LaneData>();

            foreach (var e in vehicleTypes) {
                uint laneId = segMgr.GetLaneId(SegmentId, e.Key);

                result[e.Key] = new LaneData {
                    group = new SegmentLightGroup(e.Value, endMgr.GetFlags(laneId, startNode)),
                    laneArrows = LaneArrowManager.Instance.GetFinalLaneArrows(laneId),
                    laneInfo = ExtLaneManager.Instance.GetLaneInfo(laneId),
                    defaultMask = VehicleRestrictionsManager.Instance
                                    .GetDefaultAllowedVehicleTypes(
                                        SegmentId,
                                        segmentInfo,
                                        e.Key,
                                        segmentInfo.m_lanes[e.Key],
                                        VehicleRestrictionsMode.Unrestricted),
                };
            }

            return result;
        }

        // TODO improve & remove
        public void Housekeeping(bool mayDelete, bool calculateAutoPedLight) {
#if DEBUG//HK
            bool logHouseKeeping = true;
            //bool logHouseKeeping = DebugSwitch.TimedTrafficLights.Get()
            //                       && DebugSettings.NodeId == NodeId;
#else
            const bool logHouseKeeping = false;
#endif

            NetInfo segmentInfo = SegmentId.ToSegment().Info;

            // we intentionally never delete vehicle types (because we may want to retain traffic
            // light states if a segment is upgraded or replaced)
            CustomSegmentLight mainLight = MainSegmentLight;
            ushort nodeId = NodeId;
            var setupLights = new HashSet<SegmentLightGroup>();

            var allLaneData = GetAllLaneData();

            ExtVehicleType allAllowedMask =
                Constants.ManagerFactory.VehicleRestrictionsManager.GetAllowedVehicleTypes(
                    SegmentId,
                    nodeId,
                    VehicleRestrictionsMode.Restricted);
            SeparateVehicleTypes = ExtVehicleType.None;

            LaneArrows defaultLaneArrows = allLaneData.Values.Where(ld => ld.laneInfo.m_vehicleType == VehicleInfo.VehicleType.Car
                                                                        && ld.group.VehicleType == ld.defaultMask
                                                                        && !ld.group.HasExtendedGrouping())
                                                            .Select(ld => ld.laneArrows)
                                                            .Concat(new[] { LaneArrows.None })
                                                            .Aggregate((x, y) => x | y);

            if (logHouseKeeping) {
                Log._DebugFormat(
                    "CustomSegmentLights.Housekeeping({0}, {1}): housekeeping started @ seg. {2}, " +
                    "node {3}, allLaneData={4}, allAllowedMask={5}, defaultLaneArrows={6}",
                    mayDelete,
                    calculateAutoPedLight,
                    SegmentId,
                    nodeId,
                    allLaneData.DictionaryToString(),
                    allAllowedMask,
                    defaultLaneArrows);
            }

            // bool addPedestrianLight = false;
            uint separateLanes = 0;
            int defaultLanes = 0;
            GroupsByLaneIndex = new SegmentLightGroup?[segmentInfo.m_lanes.Length];

            // TODO improve
            var laneIndicesWithoutSeparateLights = new HashSet<byte>(allLaneData.Keys);

            // check if separate traffic lights are required
            bool groupedLightsRequired = allLaneData.Values.Any(ld => ld.group.HasExtendedGrouping());

            if (!groupedLightsRequired) {
                foreach (var e in allLaneData) {
                    if (e.Value.group.VehicleType != allAllowedMask) {
                        groupedLightsRequired = true;
                        break;
                    }
                }
            }

            // set up grouped traffic lights
            if (groupedLightsRequired) {
                foreach (var e in allLaneData) {
                    byte laneIndex = e.Key;
                    NetInfo.Lane laneInfo = e.Value.laneInfo;
                    var group = e.Value.group;
                    ExtVehicleType allowedTypes = group.VehicleType;
                    ExtVehicleType defaultMask = e.Value.defaultMask;

                    Log._DebugIf(
                        logHouseKeeping,
                        () => $"CustomSegmentLights.Housekeeping({mayDelete}, {calculateAutoPedLight}): " +
                        $"housekeeping @ seg. {SegmentId}, node {nodeId}: Processing lane {laneIndex} " +
                        $"with allowedTypes={allowedTypes}, defaultMask={defaultMask}, " +
                        $"group={group}");

                    if (laneInfo.m_vehicleType == VehicleInfo.VehicleType.Car && allowedTypes == defaultMask) {
                        if (!group.HasExtendedGrouping() || (group.IsFullyAutomatic() && (e.Value.laneArrows & defaultLaneArrows) == 0)) {
                            Log._DebugIf(
                                logHouseKeeping,
                                () => $"CustomSegmentLights.Housekeeping({mayDelete}, {calculateAutoPedLight}): " +
                                $"housekeeping @ seg. {SegmentId}, node {nodeId}, lane {laneIndex}: " +
                                "Allowed types equal default mask and no extended grouping applies. Ignoring lane.");

                            // no vehicle restrictions applied, generic lights are handled further below
                            ++defaultLanes;
                            continue;
                        }
                        group.VehicleType = defaultGroup.VehicleType;
                    }

                    group.VehicleType &= ~ExtVehicleType.Emergency;

                    Log._DebugIf(
                        logHouseKeeping,
                        () => $"CustomSegmentLights.Housekeeping({mayDelete}, {calculateAutoPedLight}): " +
                        $"housekeeping @ seg. {SegmentId}, node {nodeId}, lane {laneIndex}: " +
                        $"Trying to add {group} light");

                    if (!CustomLights.TryGetValue(group, out CustomSegmentLight segmentLight)) {
                        // add a new light
                        segmentLight = new CustomSegmentLight(
                            this,
                            RoadBaseAI.TrafficLightState.Red);

                        if (mainLight != null) {
                            segmentLight.CurrentMode = mainLight.CurrentMode;
                            segmentLight.SetStates(
                                mainLight.LightMain,
                                mainLight.LightLeft,
                                mainLight.LightRight,
                                false);
                        }

                        Log._DebugIf(
                            logHouseKeeping,
                            () => $"CustomSegmentLights.Housekeeping({mayDelete}, {calculateAutoPedLight}): " +
                            $"housekeeping @ seg. {SegmentId}, node {nodeId}, lane {laneIndex}: " +
                            $"Light for group {group} does not exist. Created new light: {segmentLight} " +
                            $"(mainLight: {mainLight})");

                        CustomLights.Add(group, segmentLight);
                        Groups.AddFirst(group);
                    }

                    mainVehicleType = group.VehicleType;
                    GroupsByLaneIndex[laneIndex] = group;
                    laneIndicesWithoutSeparateLights.Remove(laneIndex);
                    ++separateLanes;

                    // addPedestrianLight = true;
                    setupLights.Add(group);
                    SeparateVehicleTypes |= group.VehicleType;

                    if (logHouseKeeping) {
                        Log._DebugFormat(
                            "CustomSegmentLights.Housekeeping({0}, {1}): housekeeping @ seg. {2}, " +
                            "node {3}: Finished processing lane {4}: mainVehicleType={5}, " +
                            "GroupsByLaneIndex={6}, laneIndicesWithoutSeparateLights={7}, " +
                            "numLights={8}, SeparateVehicleTypes={9}",
                            mayDelete,
                            calculateAutoPedLight,
                            SegmentId,
                            nodeId,
                            laneIndex,
                            mainVehicleType,
                            GroupsByLaneIndex.ArrayToString(),
                            laneIndicesWithoutSeparateLights.CollectionToString(),
                            separateLanes,
                            SeparateVehicleTypes);
                    }
                }
            }

            if (separateLanes == 0 || defaultLanes > 0) {
                Log._DebugIf(
                    logHouseKeeping,
                    () => $"CustomSegmentLights.Housekeeping({mayDelete}, {calculateAutoPedLight}): " +
                    $"housekeeping @ seg. {SegmentId}, node {nodeId}: Adding default main vehicle " +
                    $"light: {DEFAULT_MAIN_VEHICLETYPE}");

                // generic traffic lights
                if (!CustomLights.TryGetValue(
                        defaultGroup,
                        out CustomSegmentLight defaultSegmentLight)) {
                    defaultSegmentLight = new CustomSegmentLight(
                        this,
                        RoadBaseAI.TrafficLightState.Red);

                    if (mainLight != null) {
                        defaultSegmentLight.CurrentMode = mainLight.CurrentMode;
                        defaultSegmentLight.SetStates(
                            mainLight.LightMain,
                            mainLight.LightLeft,
                            mainLight.LightRight,
                            false);
                    }

                    CustomLights.Add(defaultGroup, defaultSegmentLight);
                    Groups.AddFirst(defaultGroup);
                }

                mainVehicleType = defaultGroup.VehicleType;
                setupLights.Add(defaultGroup);

                foreach (byte laneIndex in laneIndicesWithoutSeparateLights) {
                    GroupsByLaneIndex[laneIndex] = new SegmentLightGroup(ExtVehicleType.None);
                }

                Log._DebugIf(
                    logHouseKeeping,
                    () => $"CustomSegmentLights.Housekeeping({mayDelete}, {calculateAutoPedLight}): " +
                    $"housekeeping @ seg. {SegmentId}, node {nodeId}: Added default main vehicle " +
                    $"light: {defaultSegmentLight}");

                // addPedestrianLight = true;
            } else {
                // addPedestrianLight = allAllowedMask == ExtVehicleType.None
                //     || (allAllowedMask & ~ExtVehicleType.RailVehicle) != ExtVehicleType.None;
            }

            if (logHouseKeeping) {
                Log._DebugFormat(
                    "CustomSegmentLights.Housekeeping({0}, {1}): housekeeping @ seg. {2}, node {3}: " +
                    "Created all necessary lights. GroupsByLaneIndex={4}, CustomLights={5}",
                    mayDelete,
                    calculateAutoPedLight,
                    SegmentId,
                    nodeId,
                    GroupsByLaneIndex.ArrayToString(),
                    CustomLights.DictionaryToString());
            }

            if (mayDelete) {
                // delete traffic lights for non-existing vehicle-separated configurations
                var groupsToDelete = new HashSet<SegmentLightGroup>();

                foreach (SegmentLightGroup key in CustomLights.Keys) {
                    // if (e.Key == DEFAULT_MAIN_VEHICLETYPE) {
                    //        continue;
                    // }
                    if (!setupLights.Contains(key)) {
                        groupsToDelete.Add(key);
                    }
                }

                if (logHouseKeeping) {
                    Log._DebugFormat(
                        "CustomSegmentLights.Housekeeping({0}, {1}): housekeeping @ seg. {2}, " +
                        "node {3}: Going to delete unnecessary lights now: vehicleTypesToDelete={4}",
                        mayDelete,
                        calculateAutoPedLight,
                        SegmentId,
                        nodeId,
                        groupsToDelete.CollectionToString());
                }

                foreach (SegmentLightGroup group in groupsToDelete) {
                    CustomLights.Remove(group);
                    Groups.Remove(group);
                }
            }

            if (CustomLights.ContainsKey(defaultGroup)
                && Groups.First.Value != defaultGroup) {
                Groups.Remove(defaultGroup);
                Groups.AddFirst(defaultGroup);
            }

            // if (addPedestrianLight) {
            Log._DebugIf(
                logHouseKeeping,
                () => $"CustomSegmentLights.Housekeeping({mayDelete}, {calculateAutoPedLight}): " +
                $"housekeeping @ seg. {SegmentId}, node {nodeId}: adding pedestrian light");

            if (InternalPedestrianLightState == null) {
                InternalPedestrianLightState = RoadBaseAI.TrafficLightState.Red;
            }

            // } else {
            //        InternalPedestrianLightState = null;
            // }
            OnChange(calculateAutoPedLight);

            if (logHouseKeeping) {
                Log._DebugFormat(
                    "CustomSegmentLights.Housekeeping({0}, {1}): housekeeping @ seg. {2}, node {3}: " +
                    "Housekeeping complete. GroupsByLaneIndex={4} CustomLights={5}",
                    mayDelete,
                    calculateAutoPedLight,
                    SegmentId,
                    nodeId,
                    GroupsByLaneIndex.ArrayToString(),
                    CustomLights.DictionaryToString());
            }
        } // end Housekeeping()
    } // end class
}