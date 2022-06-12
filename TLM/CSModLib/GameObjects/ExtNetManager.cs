using ColossalFramework;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CSModLib.GameObjects {

    [HarmonyPatch(typeof(NetManager))]
    public sealed class ExtNetManager {

        public const int MAX_LANE_GROUP_COUNT = NetManager.MAX_SEGMENT_COUNT * 3;

        internal readonly NetSegment[] segments;
        internal readonly NetLane[] lanes;

        internal readonly ExtNetSegment[] extSegments = new ExtNetSegment[NetManager.MAX_SEGMENT_COUNT];
        internal readonly ulong[] validSegments = new ulong[(NetManager.MAX_SEGMENT_COUNT + 63) / 64];

        internal readonly ExtNetLane[] extLanes = new ExtNetLane[NetManager.MAX_LANE_COUNT];

        internal readonly Array32<NetLaneGroup> laneGroups = new Array32<NetLaneGroup>(MAX_LANE_GROUP_COUNT);

        internal static readonly ExtNetManager Instance = new ExtNetManager();

        private ExtNetManager() {
            var netManager = Singleton<NetManager>.instance;
            segments = netManager.m_segments.m_buffer;
            lanes = netManager.m_lanes.m_buffer;
        }

        public void UpdateSegment(ushort segmentId) {
            var segBucket = segmentId >> 6;
            var segBit = 1UL << (segmentId & 0x3F);
            validSegments[segBucket] &= ~segBit;
        }

        private void OnReleasedSegment(ushort segmentId) {
            var laneGroupId = extSegments[segmentId].m_laneGroup0;
            while (laneGroupId != 0) {
                var nextLaneGroupId = laneGroups.m_buffer[laneGroupId].m_nextLaneGroup;
                laneGroups.ReleaseItem(laneGroupId);
                laneGroupId = nextLaneGroupId;
            }
            UpdateSegment(segmentId);
        }

        private void CheckSegment(ushort segmentId) {
            var segBucket = segmentId >> 6;
            var segBit = 1UL << (segmentId & 0x3F);
            if (segmentId != 0 && (validSegments[segBucket] & segBit) == 0) {
                extSegments[segmentId].Initialize(this, segmentId);
                validSegments[segBucket] |= segBit;
            }
        }

        public ref ExtNetSegment GetSegment(ushort segmentId) {
            CheckSegment(segmentId);
            return ref extSegments[segmentId];
        }

        public ref ExtNetLane GetLane(uint laneId) {
            CheckSegment(lanes[laneId].m_segment);
            return ref extLanes[laneId];
        }

        public ref NetLaneGroup GetLaneGroup(uint laneGroupId) {
            CheckSegment(laneGroups.m_buffer[laneGroupId].m_segment);
            return ref laneGroups.m_buffer[laneGroupId];
        }

        [HarmonyPostfix]
        [HarmonyPatch(
            "ReleaseSegmentImplementation",
            new[] { typeof(ushort), typeof(NetSegment), typeof(bool) },
            new[] { ArgumentType.Normal, ArgumentType.Ref, ArgumentType.Normal }
        )]
        internal static void ReleaseSegmentImplementationPostfix(ushort segment) => Instance.OnReleasedSegment(segment);
    }
}
