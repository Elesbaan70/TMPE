using CSUtil.Commons;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TrafficManager.API.Traffic.Enums;
using TrafficManager.Patch;
using TrafficManager.State.ConfigData;
using TrafficManager.Util;
using TrafficManager.Util.Extensions;

namespace TrafficManager.Manager.Impl {
    internal class LaneEndManager : AbstractCustomManager {

        private static readonly object lockObject = new object();

        private readonly LaneEnd[] laneEnds;

        static LaneEndManager() {
            Instance = new LaneEndManager();
        }

        private LaneEndManager() {
            laneEnds = new LaneEnd[NetManager.MAX_LANE_COUNT * 2];

            for (uint laneId = 1; laneId < NetManager.MAX_LANE_COUNT; laneId++) {
                laneEnds[GetIndex(laneId, false)] = new LaneEnd(laneId, false);
                laneEnds[GetIndex(laneId, true)] = new LaneEnd(laneId, true);
            }

            NetManagerEvents.Instance.ReleasedLane += ReleasedLane;
            LaneConnectionManager.Instance.ConnectionsChanged += ConnectionsChanged;
        }

        public static LaneEndManager Instance { get; }

        private ref LaneEnd this[uint laneId, bool startNode] => ref laneEnds[GetIndex(laneId, startNode)];

        public LaneEndFlags GetFlags(uint laneId, bool startNode) => this[laneId, startNode].GetFlags(laneId, startNode);

        public override void OnLevelUnloading() {
            base.OnLevelUnloading();
            for (uint laneid = 1; laneid < NetManager.MAX_LANE_COUNT; laneid++) {
                this[laneid, false].Reset(laneid, false);
                this[laneid, true].Reset(laneid, true);
            }
        }

        protected override void InternalPrintDebugInfo() {
            base.InternalPrintDebugInfo();
            Log._Debug($"Lane End data:");

            for (uint laneId = 1; laneId < NetManager.MAX_LANE_COUNT; ++laneId) {
                foreach (bool startNode in new[] { true, false }) {
                    ref LaneEnd laneEnd = ref this[laneId, startNode];
                    if (laneEnd.IsValidWithSegment(laneId, startNode))
                        Log._Debug(laneEnd.ToString(laneId, startNode));
                }
            }
        }

        // If we see evidence of performance-impacting collisions,
        // this could be enhanced to use objects from a static array
        // based on an ID-derived index.
        private static object GetLockObject(uint laneId) => lockObject;

        private int GetIndex(uint laneId, bool startNode) => (int)laneId * 2 + (startNode ? 0 : 1);

        private void ReleasedLane(uint laneId) {
            this[laneId, false].Reset(laneId, false);
            this[laneId, true].Reset(laneId, true);
        }

        private void ConnectionsChanged(uint laneId, bool startNode) => this[laneId, startNode].Reset(laneId, startNode);

        private struct LaneEnd {
            /// <summary>
            /// This lane's index in the associated segment.
            /// </summary>
            private LaneEndFlags flags;

            internal LaneEnd(uint laneId, bool startNode) {
                flags = default;
            }

            public override string ToString() {
                return $"[LaneEnd\n" +
                        $"\flags={flags}\n" +
                        "LaneEnd]";
            }

            internal string ToString(uint laneId, bool startNode) {
                return $"[LaneEnd {laneId}, {startNode} on segment {laneId.ToLane().m_segment}\n" +
                        $"\tflags={flags}\n" +
                        "LaneEnd]";
            }

            internal bool IsValidWithSegment(uint laneId, bool startNode) => laneId.ToLane().IsValidWithSegment();

            internal LaneEndFlags GetFlags(uint laneId, bool startNode) => CheckFlags(laneId, startNode) ? flags : default;

            internal void Reset(uint laneId, bool startNode) {
                flags = default;
            }

            private bool Recalculate(uint laneId, bool startNode) {
#if DEBUG
                bool log = true;// DebugSwitch.LaneEnds.Get();
#else
                const bool log = false;
#endif

                if (log) {
                    Log._Debug($"LaneEnd.Recalculate({laneId}, {startNode}) called.");
                }

                ref var lane = ref laneId.ToLane();
                if (lane.IsValidWithSegment()) {

                    if (log) {
                        Log._Debug($"LaneEnd.Recalculate: lane is valid with segment {laneId.ToLane().m_segment}");
                    }

                    flags = default;

                    if (LaneConnectionManager.Instance.HasConnections(laneId, startNode)) {

                        ref var segment = ref lane.m_segment.ToSegment();
                        ushort nodeId = startNode ? segment.m_startNode : segment.m_endNode;
                        ref var node = ref nodeId.ToNode();
                        int laneIndex = ExtLaneManager.Instance.GetLaneIndex(laneId);
                        bool isDisplacedLane = segment.Info.IsDisplacedLane(laneIndex);
                        var farDirection = Shortcuts.LHT ? ArrowDirection.Right : ArrowDirection.Left;

                        if (log) {
                            Log._Debug($"LaneEnd.Recalculate: connections found: nodeId={nodeId}, laneIndex={laneIndex}, isDisplacedLane={isDisplacedLane}, farDirection={farDirection}");
                        }

                        foreach (var connectionsBySegment in LaneConnectionManager.Instance.GetLaneConnections(laneId, startNode)
                                                                .GroupBy(l => l.ToLane().m_segment)) {

                            var otherSegmentId = connectionsBySegment.Key;
                            ref var otherSegment = ref otherSegmentId.ToSegment();
                            var otherHasDisplacedLanes = otherSegment.Info.HasDisplacedLanes();

                            if (log) {
                                Log._Debug($"LaneEnd.Recalculate: otherSegmentId={otherSegmentId}, otherHasDisplacedLanes={otherHasDisplacedLanes}");
                            }

                            if (isDisplacedLane || otherHasDisplacedLanes) {

                                var dir = ExtSegmentEndManager.Instance.GetDirection(lane.m_segment, otherSegmentId, nodeId);

                                if (dir == farDirection || dir == ArrowDirection.Forward) {

                                    foreach (var otherLaneId in connectionsBySegment) {
                                        int otherLaneIndex = ExtLaneManager.Instance.GetLaneIndex(otherLaneId);
                                        bool isOtherDisplacedLane = otherHasDisplacedLanes && otherSegment.Info.IsDisplacedLane(otherLaneIndex);

                                        if (log) {
                                            Log._Debug($"LaneEnd.Recalculate: dir={dir}, otherLaneIndex={otherLaneIndex}, isOtherDisplacedLane={isOtherDisplacedLane}");
                                        }

                                        if (dir == farDirection) {

                                            if (isDisplacedLane)
                                                flags |= LaneEndFlags.TurnOutOfDisplaced;

                                            if (isOtherDisplacedLane)
                                                flags |= LaneEndFlags.TurnIntoDisplaced;

                                        } else if (dir == ArrowDirection.Forward) {

                                            if (isDisplacedLane) {
                                                if (isOtherDisplacedLane) {
                                                    flags |= LaneEndFlags.ForwardDisplaced;
                                                } else {
                                                    flags |= Shortcuts.LHT ? LaneEndFlags.CrossLeft : LaneEndFlags.CrossRight;
                                                }
                                            } else if (isOtherDisplacedLane) {
                                                flags |= Shortcuts.LHT ? LaneEndFlags.CrossRight : LaneEndFlags.CrossLeft;
                                            }
                                        }
                                    }
                                } else if (log) {
                                    Log._Debug($"LaneEnd.Recalculate: nothing to do on this segment: dir={dir}");
                                }
                            } else if (log) {
                                Log._Debug($"LaneEnd.Recalculate: nothing to do on this segment: no displaced lanes");
                            }
                        }
                    } else if (log) {
                        Log._Debug($"LaneEnd.Recalculate: lane end has no connections");
                    }


                    flags |= LaneEndFlags.Initialized;

                    if (log) {
                        Log._Debug($"LaneEnd.Recalculate: finishing with flags={flags}");
                    }

                    return true;
                }
                Reset(laneId, startNode);
                if (log) {
                    Log._Debug($"LaneEnd.Recalculate: finishing with flags={flags}");
                }
                return false;
            }

            private bool CheckFlags(uint laneId, bool startNode) {
                if ((flags & LaneEndFlags.Initialized) == 0) {
                    lock (GetLockObject(laneId)) {
                        if ((flags & LaneEndFlags.Initialized) == 0) {
                            return Recalculate(laneId, startNode);
                        }
                    }
                }
                return true;
            }
        }
    }
}
