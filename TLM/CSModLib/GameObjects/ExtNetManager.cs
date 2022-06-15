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
        internal readonly NetNode[] nodes;

        internal readonly ExtNetSegment[] extSegments = new ExtNetSegment[NetManager.MAX_SEGMENT_COUNT];
        internal readonly ulong[] validSegments = new ulong[(NetManager.MAX_SEGMENT_COUNT + 63) / 64];
        internal readonly ulong[] validNodesOnSegments = new ulong[(NetManager.MAX_SEGMENT_COUNT + 63) / 64];

        internal readonly ulong[] validNodes = new ulong[(NetManager.MAX_NODE_COUNT + 63) / 64];

        internal readonly ExtNetLane[] extLanes = new ExtNetLane[NetManager.MAX_LANE_COUNT];

        internal readonly Array32<NetLaneGroup> laneGroups = new Array32<NetLaneGroup>(MAX_LANE_GROUP_COUNT);

        internal static readonly ExtNetManager Instance = new ExtNetManager();

        private ExtNetManager() {
            var netManager = Singleton<NetManager>.instance;
            segments = netManager.m_segments.m_buffer;
            lanes = netManager.m_lanes.m_buffer;
            nodes = netManager.m_nodes.m_buffer;
        }

        public void UpdateNode(ushort nodeId) {
            if (nodeId != 0) {
                var nodeBucket = nodeId >> 6;
                var nodeBit = 1UL << (nodeId & 0x3F);
                validNodes[nodeBucket] &= ~nodeBit;
                ref var node = ref nodes[nodeId];
                for (int i = 0; i < 8; i++) {
                    var segmentId = node.GetSegment(i);
                    var segmentBucket = segmentId >> 6;
                    var segmentBit = 1UL << (segmentId & 0x3F);
                    validNodesOnSegments[segmentBucket] &= ~segmentBit;
                }
            }
        }

        public void UpdateSegment(ushort segmentId) => UpdateSegment(segmentId, true, true);

        public void UpdateSegment(ushort segmentId, bool info, bool node) {
            if (segmentId != 0) {
                var segBucket = segmentId >> 6;
                var segBit = 1UL << (segmentId & 0x3F);
                if (info)
                    validSegments[segBucket] &= ~segBit;

                if (node) {
                    validNodesOnSegments[segBucket] &= ~segBit;
                    ref var segment = ref segments[segmentId];
                    UpdateNode(segment.m_startNode);
                    UpdateNode(segment.m_endNode);
                }
            }
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

        private void CheckNode(ushort nodeId) {
            if (nodeId != 0) {
                var nodeBucket = nodeId >> 6;
                var nodeBit = 1UL << (nodeId & 0x3F);
                if ((validNodes[nodeBucket] & nodeBit) == 0) {
                    ref var node = ref nodes[nodeId];
                    for (int i = 0; i < 8; i++) {
                        var segmentId = node.GetSegment(i);
                        if (segmentId != 0) {
                            CheckSegment(segmentId, false);
                        }
                    }
                    // TODO: initialize hypothetical ExtNetNode here
                    validNodes[nodeBucket] |= nodeBit;
                }
            }
        }

        private void CheckSegment(ushort segmentId, bool checkNode = true) {
            if (segmentId != 0) {
                var segBucket = segmentId >> 6;
                var segBit = 1UL << (segmentId & 0x3F);
                if ((validSegments[segBucket] & segBit) == 0) {
                    extSegments[segmentId].Initialize(this, segmentId);
                    validSegments[segBucket] |= segBit;
                }
                if (checkNode && (validNodesOnSegments[segBucket] & segBit) == 0) {
                    ref var segment = ref segments[segmentId];
                    CheckNode(segment.m_startNode);
                    CheckNode(segment.m_endNode);
                }
            }
        }

        public ref ExtNetSegment GetExtSegment(ushort segmentId) {
            CheckSegment(segmentId);
            return ref extSegments[segmentId];
        }

        public ref ExtNetLane GetExtLane(uint laneId) {
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
        internal static void ReleaseSegmentImplementationPostfix(ushort segment, ref NetSegment data, bool keepNodes)
            => Instance.OnReleasedSegment(segment);

        [HarmonyPostfix]
        [HarmonyPatch(
            "UpdateSegment",
            new[] { typeof(ushort), typeof(ushort), typeof(ushort) },
            new[] { ArgumentType.Normal, ArgumentType.Normal, ArgumentType.Normal }
        )]
        internal static void UpdateSegmentPostfix(ushort segment, ushort fromNode, int level)
            => Instance.UpdateSegment(segment, true, false);

        [HarmonyPostfix]
        [HarmonyPatch(
            "UpdateNode",
            new[] { typeof(ushort), typeof(ushort), typeof(ushort) },
            new[] { ArgumentType.Normal, ArgumentType.Normal, ArgumentType.Normal }
        )]
        internal static void UpdateNodePostfix(ushort node, ushort fromSegment, int level)
            => Instance.UpdateNode(node);
    }
}
