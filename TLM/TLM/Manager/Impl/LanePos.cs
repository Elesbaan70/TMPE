namespace TrafficManager.Manager.Impl {
    public struct LanePos {
        public uint laneId;
        public byte laneIndex;
        public float position;
        public VehicleInfo.VehicleType vehicleType;
        public NetInfo.LaneType laneType;
        public NetInfo.Direction finalDirection;

        public LanePos(uint laneId,
                       byte laneIndex,
                       float position,
                       VehicleInfo.VehicleType vehicleType,
                       NetInfo.LaneType laneType,
                       NetInfo.Direction finalDirection) {
            this.laneId = laneId;
            this.laneIndex = laneIndex;
            this.position = position;
            this.vehicleType = vehicleType;
            this.laneType = laneType;
            this.finalDirection = finalDirection;
        }
    }
}
