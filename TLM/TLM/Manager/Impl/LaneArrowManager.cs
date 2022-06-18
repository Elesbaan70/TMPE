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

    public partial class LaneArrowManager
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

        private readonly LegacyLaneArrowManager Legacy = new LegacyLaneArrowManager();

        protected override void InternalPrintDebugInfo() {
            base.InternalPrintDebugInfo();
            Log.NotImpl("InternalPrintDebugInfo for LaneArrowManager");
        }

        public LaneArrows GetFinalLaneArrows(uint laneId) => Legacy.GetFinalLaneArrows(laneId);

        protected override void HandleInvalidSegment(ref ExtSegment seg) {
            Legacy.HandleInvalidSegment(ref seg);
        }

        protected override void HandleValidSegment(ref ExtSegment seg) { }

        public override void OnLevelLoading() {
            base.OnLevelLoading();
            Legacy.OnLevelLoading();
        }

        public override void OnBeforeSaveData() {
            base.OnBeforeSaveData();
            Legacy.OnBeforeSaveData();
        }

        public override void OnAfterLoadData() {
            base.OnAfterLoadData();
            Legacy.OnAfterLoadData();
        }

        [Obsolete]
        public bool LoadData(string data) => Legacy.LoadData(data);

        [Obsolete]
        string ICustomDataManager<string>.SaveData(ref bool success) => null;

        public bool LoadData(List<Configuration.LaneArrowData> data) => Legacy.LoadData(data);

        public List<Configuration.LaneArrowData> SaveData(ref bool success) => Legacy.SaveData(ref success);

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

        /// <summary>
        /// Toggles a lane arrows (user or default) on and off for the directions where flag is set.
        /// overrides default settings for the arrows that change.
        /// default arrows may change as user connects or remove more segments to the junction but
        /// the user arrows stay the same no matter what.
        /// </summary>
        public bool ToggleLaneArrows(uint laneId, bool startNode, LaneArrows flags, out SetLaneArrow_Result res)
            => Legacy.ToggleLaneArrows(laneId, startNode, flags, out res);

        /// <summary>
        /// Set the lane arrows to flags. this will remove all default arrows for the lane
        /// and replace it with user arrows.
        /// default arrows may change as user connects or remove more segments to the junction but
        /// the user arrows stay the same no matter what.
        /// </summary>
        public bool SetLaneArrows(uint laneId, LaneArrows flags, bool overrideHighwayArrows = false)
            => Legacy.SetLaneArrows(laneId, flags, overrideHighwayArrows);

        /// <summary>
        /// Resets lane arrows to their default value for the given segment end.
        /// </summary>
        /// <param name="segmentId">Segment id.</param>
        /// <param name="startNode">Determines the segment end to reset, or both if <c>null</c>.</param>
        public void ResetLaneArrows(ushort segmentId, bool? startNode = null) => Legacy.ResetLaneArrows(segmentId, startNode);

        /// <summary>
        /// Updates all road relevant segments so that the dedicated turning lane policy would take effect.
        /// </summary>
        /// <param name="recalculateRoutings">
        /// also recalculate lane transitions in routing manager.
        /// Current car paths will not be recalculated (to save time). New paths will follow the new lane routings.
        /// </param>
        public void UpdateDedicatedTurningLanePolicy(bool recalculateRoutings)
            => Legacy.UpdateDedicatedTurningLanePolicy(recalculateRoutings);

        /// <summary>
        /// remove arrows (user or default) where flag is set.
        /// default arrows may change as user connects or remove more segments to the junction but
        /// the user arrows stay the same no matter what.
        /// </summary>
        public bool RemoveLaneArrows(uint laneId, LaneArrows flags, bool overrideHighwayArrows = false)
            => Legacy.RemoveLaneArrows(laneId, flags, overrideHighwayArrows);

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
        public bool AddLaneArrows(uint laneId, LaneArrows flags, bool overrideHighwayArrows = false)
            => Legacy.AddLaneArrows(laneId, flags, overrideHighwayArrows);

        internal LaneArrows? GetLaneArrowFlags(uint laneId) => Legacy.GetLaneArrowFlags(laneId);

        internal bool ApplyLaneArrowFlags(uint laneId, bool check = true) => Legacy.ApplyLaneArrowFlags(laneId, check);

        internal void RemoveLaneArrowFlags(uint laneId) => Legacy.RemoveLaneArrowFlags(laneId);

        internal bool CanHaveLaneArrows(uint laneId, bool? startNode = null) => Legacy.CanHaveLaneArrows(laneId, startNode);

        internal void ClearHighwayLaneArrows() => Legacy.ClearHighwayLaneArrows();

        internal void RemoveHighwayLaneArrowFlags(uint laneId) => Legacy.RemoveHighwayLaneArrowFlags(laneId);

        internal LaneArrows? GetHighwayLaneArrowFlags(uint laneId) => Legacy.GetHighwayLaneArrowFlags(laneId);

        internal void SetHighwayLaneArrowFlags(uint laneId, LaneArrows flags, bool check = true)
            => Legacy.SetHighwayLaneArrowFlags(laneId, flags, check);

        internal void RemoveHighwayLaneArrowFlagsAtSegment(ushort segmentId) => Legacy.RemoveHighwayLaneArrowFlagsAtSegment(segmentId);

    }
}