using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace CSModLib.GameObjects {
    public struct ExtNetSegment {

        public ushort m_segment;

        public uint m_firstSortedLane;
        public uint m_lastSortedLane;

        public uint m_lane0;
        public uint m_lane1;
        public uint m_lane2;
        public uint m_lane3;
        public uint m_lane4;
        public uint m_lane5;
        public uint m_lane6;
        public uint m_lane7;
        public uint m_lane8;
        public uint m_lane9;
        public uint m_lane10;
        public uint m_lane11;
        public uint m_lane12;
        public uint m_lane13;
        public uint m_lane14;
        public uint m_lane15;

        public uint m_laneGroup0;
        public uint m_laneGroup1;
        public uint m_laneGroup2;
        public uint m_laneGroup3;

        public uint m_lastLaneGroup;

        [ThreadStatic]
        private static uint[] idCollection;

        public uint GetLaneId(int laneIndex) {
            switch (laneIndex) {
                case < 0: return 0;
                case 0: return m_lane0;
                case 1: return m_lane1;
                case 2: return m_lane2;
                case 3: return m_lane3;
                case 4: return m_lane4;
                case 5: return m_lane5;
                case 6: return m_lane6;
                case 7: return m_lane7;
                case 8: return m_lane8;
                case 9: return m_lane9;
                case 10: return m_lane10;
                case 11: return m_lane11;
                case 12: return m_lane12;
                case 13: return m_lane13;
                case 14: return m_lane14;
                case 15: return m_lane15;
            }
            uint result = m_lane15;
            for (int i = 15; i < laneIndex && result != 0; i++) {
                result = ExtNetManager.Instance.lanes[result].m_nextLane;
            }
            return result;
        }

        public uint GetLaneGroupId(int laneGroupIndex) {
            switch (laneGroupIndex) {
                case < 0: return 0;
                case 0: return m_laneGroup0;
                case 1: return m_laneGroup1;
                case 2: return m_laneGroup2;
                case 3: return m_laneGroup3;
            }
            uint result = m_laneGroup3;
            for (int i = 3; i < laneGroupIndex && result != 0; i++) {
                result = ExtNetManager.Instance.laneGroups.m_buffer[result].m_nextLaneGroup;
            }
            return result;
        }

        internal void Initialize(ExtNetManager extNetManager, ushort segmentId) {

            idCollection ??= new uint[256];

            m_segment = segmentId;

            ref var segment = ref extNetManager.segments[segmentId];
            var info = segment.Info;
            var extInfo = ExtNetInfo.GetInstance(info);

            InitLanes(extNetManager, segment, extInfo);
            InitLaneGroups(extNetManager, extInfo);
        }

        private void InitLanes(ExtNetManager extNetManager, NetSegment segment, ExtNetInfo extInfo) {
            int laneCount = extInfo.m_sortedLanes.Length;
            if (laneCount == 0) {
                m_firstSortedLane = m_lastSortedLane = 0;
            } else {
                uint laneId = segment.m_lanes;
                ref var extLane = ref extNetManager.extLanes[laneId];
                for (int laneIndex = 0; laneId != 0 && laneIndex < laneCount; laneIndex++, laneId = extNetManager.lanes[laneId].m_nextLane) {
                    extLane = ref extNetManager.extLanes[laneId];
                    idCollection[laneIndex] = extLane.m_lane = laneId;
                    extLane.m_laneIndex = laneIndex;
                    extLane.m_prevGroupLane = extLane.m_nextGroupLane = 0;
                }

                uint prevLaneId = 0;
                m_firstSortedLane = laneId = idCollection[extInfo.m_sortedLanes[0]];
                extLane = ref extNetManager.extLanes[laneId];
                for (int sortedIndex = 1; sortedIndex < laneCount; sortedIndex++) {
                    uint nextLaneId = idCollection[extInfo.m_sortedLanes[sortedIndex]];
                    extLane.m_prevSortedLane = prevLaneId;
                    extLane.m_nextSortedLane = nextLaneId;
                    prevLaneId = laneId;
                    laneId = nextLaneId;
                    extLane = ref extNetManager.extLanes[laneId];
                }
                m_lastSortedLane = extLane.m_prevSortedLane = prevLaneId;
                extLane.m_nextSortedLane = 0;
            }

            for (int i = laneCount; i < 16; i++)
                idCollection[i] = 0;
            m_lane0 = idCollection[0];
            m_lane1 = idCollection[1];
            m_lane2 = idCollection[2];
            m_lane3 = idCollection[3];
            m_lane4 = idCollection[4];
            m_lane5 = idCollection[5];
            m_lane6 = idCollection[6];
            m_lane7 = idCollection[7];
            m_lane8 = idCollection[8];
            m_lane9 = idCollection[9];
            m_lane10 = idCollection[10];
            m_lane11 = idCollection[11];
            m_lane12 = idCollection[12];
            m_lane13 = idCollection[13];
            m_lane14 = idCollection[14];
            m_lane15 = idCollection[15];
        }

        private void InitLaneGroups(ExtNetManager extNetManager, ExtNetInfo extInfo) {
            int laneGroupCount = extInfo.m_laneGroups.Length;
            if (laneGroupCount == 0) {
                m_lastLaneGroup = 0;
            } else {
                uint laneGroupId;
                for (int laneGroupIndex = 0; laneGroupIndex < laneGroupCount; laneGroupIndex++) {
                    if (extNetManager.laneGroups.CreateItem(out laneGroupId)) {
                        idCollection[laneGroupIndex] = laneGroupId;
                        extNetManager.laneGroups.m_buffer[laneGroupId].Initialize(
                            extNetManager, extInfo.m_laneGroups[laneGroupIndex], ref this, laneGroupId, laneGroupIndex);
                    }
                }

                uint prevLaneGroupId = 0;
                laneGroupId = idCollection[extInfo.m_sortedLanes[0]];
                ref var laneGroup = ref extNetManager.laneGroups.m_buffer[laneGroupId];
                for (int laneGroupIndex = 1; laneGroupIndex < laneGroupCount; laneGroupIndex++) {
                    uint nextLaneGroupId = idCollection[laneGroupIndex];
                    laneGroup.m_prevLaneGroup = prevLaneGroupId;
                    laneGroup.m_nextLaneGroup = nextLaneGroupId;
                    prevLaneGroupId = laneGroupId;
                    laneGroupId = nextLaneGroupId;
                    laneGroup = ref extNetManager.laneGroups.m_buffer[laneGroupId];
                }
                m_lastLaneGroup = laneGroup.m_prevLaneGroup = prevLaneGroupId;
                laneGroup.m_nextLaneGroup = 0;
            }

            for (int i = laneGroupCount; i < 4; i++)
                idCollection[i] = 0;
            m_laneGroup0 = idCollection[0];
            m_laneGroup1 = idCollection[1];
            m_laneGroup2 = idCollection[2];
            m_laneGroup3 = idCollection[3];
        }
    }
}
