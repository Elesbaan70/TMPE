using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CSModLib.GameObjects {
    public struct ExtNetLane {

        public uint m_lane;
        public int m_laneIndex;

        public ushort m_laneGroup;

        public uint m_prevSortedLane;
        public uint m_nextSortedLane;

        public uint m_prevGroupLane;
        public uint m_nextGroupLane;

        public static ref ExtNetLane Of(uint laneId)
            => ref ExtNetManager.Instance.GetExtLane(laneId);

        public static ref NetLane NetLaneOf(uint laneId)
            => ref ExtNetManager.Instance.lanes[laneId];

        public ref NetLane NetLane => ref ExtNetManager.Instance.lanes[m_lane];
    }
}
