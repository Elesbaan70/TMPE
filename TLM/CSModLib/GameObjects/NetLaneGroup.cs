using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CSModLib.GameObjects {
    public struct NetLaneGroup {

        public ushort m_segment;
        public uint m_laneGroup;
        public int m_laneGroupIndex;

        public uint m_prevLaneGroup;
        public uint m_nextLaneGroup;

        public uint m_lane0;
        public uint m_lane1;
        public uint m_lane2;
        public uint m_lane3;
        public uint m_lane4;
        public uint m_lane5;

        public uint m_lastLane;

        [ThreadStatic]
        private static uint[] idCollection;

        public static ref NetLaneGroup Of(uint laneGroupId) => ref ExtNetManager.Instance.GetLaneGroup(laneGroupId);

        public uint GetLeftLaneGroup(ushort nodeId) {
            ref var segment = ref ExtNetManager.Instance.segments[m_segment];
            bool startNode = segment.m_startNode == nodeId;
            return startNode || segment.m_endNode == nodeId ? GetNextLaneGroup(startNode, nodeId, true) : 0;
        }

        public uint GetLeftLaneGroup(bool startNode) {
            ref var segment = ref ExtNetSegment.NetSegmentOf(m_segment);
            return GetNextLaneGroup(startNode, startNode ? segment.m_startNode : segment.m_endNode, true);
        }

        public uint GetRightLaneGroup(ushort nodeId) {
            ref var segment = ref ExtNetSegment.NetSegmentOf(m_segment);
            bool startNode = segment.m_startNode == nodeId;
            return startNode || segment.m_endNode == nodeId ? GetNextLaneGroup(startNode, nodeId, false) : 0;
        }

        public uint GetRightLaneGroup(bool startNode) {
            ref var segment = ref ExtNetSegment.NetSegmentOf(m_segment);
            return GetNextLaneGroup(startNode, startNode ? segment.m_startNode : segment.m_endNode, false);
        }

        private uint GetNextLaneGroup(bool startNode, ushort nodeId, bool left) {
            if (startNode ^ left) {
                if (m_prevLaneGroup != 0)
                    return m_prevLaneGroup;
            } else {
                if (m_nextLaneGroup != 0)
                    return m_nextLaneGroup;
            }

            var segments = ExtNetManager.Instance.segments;
            var extSegments = ExtNetManager.Instance.extSegments;

            var segmentId = startNode
                            ? (left ? segments[m_segment].m_startLeftSegment : segments[m_segment].m_startRightSegment)
                            : (left ? segments[m_segment].m_endLeftSegment : segments[m_segment].m_endRightSegment);
            do {
                ref var segment = ref segments[segmentId];
                startNode = nodeId == segment.m_startNode;
                ref var extSegment = ref extSegments[segmentId];
                if (startNode ^ left) {
                    if (extSegment.m_lastLaneGroup != 0)
                        return extSegment.m_lastLaneGroup;
                } else {
                    if (extSegment.m_laneGroup0 != 0)
                        return extSegment.m_laneGroup0;
                }

                segmentId = startNode
                            ? (left ? segment.m_startLeftSegment : segment.m_startRightSegment)
                            : (left ? segment.m_endLeftSegment : segment.m_endRightSegment);
            } while (segmentId != m_segment);

            return m_laneGroup;
        }

        public uint GetLaneId(int laneIndex) {
            switch (laneIndex) {
                case < 0: return 0;
                case 0: return m_lane0;
                case 1: return m_lane1;
                case 2: return m_lane2;
                case 3: return m_lane3;
                case 4: return m_lane4;
                case 5: return m_lane5;
            }
            uint result = m_lane5;
            for (int i = 5; i < laneIndex && result != 0; i++) {
                result = ExtNetManager.Instance.lanes[result].m_nextLane;
            }
            return result;
        }

        internal void Initialize(ExtNetManager extNetManager, ExtNetInfo.LaneGroupInfo info, ref ExtNetSegment extSegment, uint laneGroupId, int laneGroupIndex) {

            idCollection ??= new uint[256];

            m_segment = extSegment.m_segment;
            m_laneGroup = laneGroupId;
            m_laneGroupIndex = laneGroupIndex;

            int laneCount = info.m_sortedLanes.Length;
            if (laneCount == 0) {
                m_lastLane = 0;
            } else {
                uint prevLaneId = 0;
                uint laneId = idCollection[0] = extSegment.GetLaneId(info.m_sortedLanes[0]);
                ref var extLane = ref extNetManager.extLanes[laneId];
                for (int sortedIndex = 1; sortedIndex < laneCount; sortedIndex++) {
                    uint nextLaneId = idCollection[sortedIndex] = extSegment.GetLaneId(info.m_sortedLanes[sortedIndex]);
                    extLane.m_prevGroupLane = prevLaneId;
                    extLane.m_nextGroupLane = nextLaneId;
                    prevLaneId = laneId;
                    laneId = nextLaneId;
                    extLane = ref extNetManager.extLanes[laneId];
                }
                m_lastLane = extLane.m_prevGroupLane = prevLaneId;
                extLane.m_nextGroupLane = 0;
            }

            for (int i = laneCount; i < 6; i++)
                idCollection[i] = 0;
            m_lane0 = idCollection[0];
            m_lane1 = idCollection[1];
            m_lane2 = idCollection[2];
            m_lane3 = idCollection[3];
            m_lane4 = idCollection[4];
            m_lane5 = idCollection[5];
        }
    }
}
