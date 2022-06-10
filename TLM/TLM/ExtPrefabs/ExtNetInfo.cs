using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TrafficManager.Util.Extensions;

namespace TrafficManager.ExtPrefabs {
    internal partial class ExtNetInfo : ExtPrefabInfo<ExtNetInfo, NetInfo> {

        [Flags]
        public enum ExtLaneFlags {

            None = 0,

            /// <summary>
            /// Lanes outside a median (if any) that divides lanes going the same direction.
            /// In the absence of such a median, these are simply lanes that are not displaced.
            /// </summary>
            Outer = 1 << 0,

            /// <summary>
            /// Lanes inside a median that divides lanes going the same direction.
            /// </summary>
            Inner = 1 << 1,

            /// <summary>
            /// Displaced lanes that are between lanes going the opposite direction.
            /// </summary>
            DisplacedInner = 1 << 2,

            /// <summary>
            /// Displaced lanes that are located on the far side of the road from what is normal for their direction.
            /// </summary>
            DisplacedOuter = 1 << 3,

            /// <summary>
            /// Lanes in a group whose prevailing direction is forward.
            /// </summary>
            ForwardGroup = 1 << 4,

            /// <summary>
            /// Lanes in a group whose prevailing direction is backward.
            /// </summary>
            BackwardGroup = 1 << 5,

            /// <summary>
            /// Lanes that may be treated as service lanes in controlled lane routing.
            /// </summary>
            AllowServiceLane = 1 << 6,

            /// <summary>
            /// Lanes that may be treated as express lanes in controlled lane routing.
            /// </summary>
            AllowExpressLane = 1 << 7,

            /// <summary>
            /// Lanes that may be treated as displaced far turn lanes in controlled lane routing.
            /// </summary>
            AllowCFI = 1 << 8,

            /// <summary>
            /// Lanes whose properties cause controlled lane routing to be disabled for the segment.
            /// </summary>
            ForbidControlledLanes = 1 << 9,

            OuterForward = Outer | ForwardGroup,
            InnerForward = Inner | ForwardGroup,
            DisplacedInnerForward = DisplacedInner | ForwardGroup,
            DisplacedOuterForward = DisplacedOuter | ForwardGroup,

            OuterBackward = Outer | BackwardGroup,
            InnerBackward = Inner | BackwardGroup,
            DisplacedInnerBackward = DisplacedInner | BackwardGroup,
            DisplacedOuterBackward = DisplacedOuter | BackwardGroup,
        }

        /// <summary>
        /// Mask to obtain the lane grouping key.
        /// </summary>
        public const ExtLaneFlags LaneGroupingKey =
                ExtLaneFlags.Outer | ExtLaneFlags.Inner
                | ExtLaneFlags.DisplacedInner | ExtLaneFlags.DisplacedOuter
                | ExtLaneFlags.ForwardGroup | ExtLaneFlags.BackwardGroup;

        /// <summary>
        /// Mask to test a segment for the presence of service lanes.
        /// </summary>
        public const ExtLaneFlags ServiceLaneRule = ExtLaneFlags.AllowServiceLane | ExtLaneFlags.ForbidControlledLanes;

        /// <summary>
        /// Mask to test a segment for the presence of express lanes.
        /// </summary>
        public const ExtLaneFlags ExpressLaneRule = ExtLaneFlags.AllowExpressLane | ExtLaneFlags.ForbidControlledLanes;

        /// <summary>
        /// Mask to test a segment for the presence of displaced far turn lanes.
        /// </summary>
        public const ExtLaneFlags CFIRule = ExtLaneFlags.AllowCFI | ExtLaneFlags.ForbidControlledLanes;

        /// <summary>
        /// Mask to test the prevailing direction of a lane group.
        /// </summary>
        public const ExtLaneFlags LaneGroupDirection = ExtLaneFlags.ForwardGroup | ExtLaneFlags.BackwardGroup;

        /// <summary>
        /// Mask to test a segment for the presence of any displaced lanes.
        /// </summary>
        public const ExtLaneFlags DisplacedLanes = ExtLaneFlags.DisplacedInner | ExtLaneFlags.DisplacedOuter;

        /// <summary>
        /// Extended lane prefab data, directly corresponding to <see cref="NetInfo.m_lanes"/>.
        /// </summary>
        public ExtLaneInfo[] m_extLanes;

        /// <summary>
        /// Lane group prefab data.
        /// </summary>
        public LaneGroupInfo[] m_laneGroups;

        /// <summary>
        /// Lane indices sorted by position, then by direction.
        /// </summary>
        public int[] m_sortedLanes;

        /// <summary>
        /// Aggregate lane flags for forward groups.
        /// </summary>
        public ExtLaneFlags m_forwardExtLaneFlags;

        /// <summary>
        /// Aggregate lane flags for backward groups.
        /// </summary>
        public ExtLaneFlags m_backwardExtLaneFlags;

        /// <summary>
        /// Aggregate of all lane flags.
        /// </summary>
        public ExtLaneFlags m_extLaneFlags;

        public ExtNetInfo(NetInfo info)
            : this(info.m_lanes) {
        }

        private enum LaneConfiguration {
            Simple = 1 << 0,
            OneWay = 1 << 1,
            Inverted = 1 << 2,
            Complex = 1 << 3,
        }

        private const LaneConfiguration StandardTwoWayConfiguration = LaneConfiguration.Simple | LaneConfiguration.Complex;

        public ExtNetInfo(NetInfo.Lane[] lanes) {

            m_extLanes = lanes.Select(l => new ExtLaneInfo(l)).ToArray();

            var evaluator = new LaneEvaluator(this, lanes);

            evaluator.CalculateSortedLanes();

            var configuration = evaluator.GetLaneConfiguration();

            var lastDisplacedOuterBackward = lanes.Length;
            var lastDisplacedOuterForward = -1;

            int sortedIndex;

            if (configuration != LaneConfiguration.Complex) {
                for (int i = 0; i < m_extLanes.Length; i++) {
                    if (lanes[i].IsRoadLane())
                        m_extLanes[i].m_extFlags |= ExtLaneFlags.Outer;
                }
            } else {

                evaluator.FindDisplacedOuter(ExtLaneFlags.ForwardGroup, ref lastDisplacedOuterForward);
                evaluator.FindDisplacedOuter(ExtLaneFlags.BackwardGroup, ref lastDisplacedOuterBackward);

                evaluator.FindOuterAndDisplacedInner(ExtLaneFlags.ForwardGroup);
                evaluator.FindOuterAndDisplacedInner(ExtLaneFlags.BackwardGroup);
            }

            if ((configuration & StandardTwoWayConfiguration) != 0) {

                var workingDirection = ExtLaneFlags.ForwardGroup;
                var oppositeDirection = ExtLaneFlags.BackwardGroup;
                var scanStart = lastDisplacedOuterBackward - 1;
                var scanEnd = lastDisplacedOuterForward;
                var step = -1;

                for (int iteration = 0; iteration < 2; iteration++) {

                    // advance to first Outer

                    float laneWidth = float.MaxValue;
                    float laneEdge = float.MaxValue * step;
                    float laneElevation = float.MaxValue;
                    float medianEdge = float.MaxValue * step;
                    float medianElevation = float.MinValue;

                    int firstOuterLane;
                    int firstInnerLane = -1;

                    for (firstOuterLane = scanStart; firstOuterLane != scanEnd; firstOuterLane += step) {

                        var laneIndex = m_sortedLanes[firstOuterLane];
                        if ((m_extLanes[laneIndex].m_extFlags & workingDirection) != 0) {
                            var lane = lanes[laneIndex];
                            laneWidth = lane.m_width;
                            laneEdge = lane.m_position + laneWidth / 2 * step;
                            laneElevation = lane.m_verticalOffset;
                            break;
                        }
                    }

                    // scan for median

                    for (sortedIndex = firstOuterLane + step; sortedIndex != scanEnd; sortedIndex += step) {
                        var laneIndex = m_sortedLanes[sortedIndex];
                        var extLane = m_extLanes[laneIndex];
                        var lane = lanes[laneIndex];
                        if ((extLane.m_extFlags & workingDirection) != 0) {

                            if (step < 0) {
                                if (((lane.m_position + lane.m_width / 2) <= medianEdge
                                            && lane.m_verticalOffset < medianElevation
                                            && medianElevation - lane.m_verticalOffset < 3f)
                                        || (lane.m_position + lane.m_width / 2 + lane.m_width + laneWidth) <= laneEdge) {
                                    firstInnerLane = sortedIndex;
                                    break;
                                }
                            } else {
                                if (((lane.m_position - lane.m_width / 2) >= medianEdge
                                            && lane.m_verticalOffset < medianElevation
                                            && medianElevation - lane.m_verticalOffset < 3f)
                                        || (lane.m_position - lane.m_width / 2 - lane.m_width - laneWidth) >= laneEdge) {
                                    firstInnerLane = sortedIndex;
                                    break;
                                }
                            }
                            medianEdge = float.MaxValue * step;
                            medianElevation = float.MinValue;
                            laneWidth = lane.m_width;
                            laneEdge = lane.m_position + laneWidth / 2 * step;
                            laneElevation = lane.m_verticalOffset;
                        } else if ((extLane.m_extFlags & oppositeDirection) != 0) {
                            break;
                        } else {
                            if (!lane.IsRoadLane() && lane.m_verticalOffset > laneElevation && lane.m_verticalOffset - laneElevation < 3f) {
                                if (step < 0 && (lane.m_position + lane.m_width / 2) <= laneEdge) {
                                    medianEdge = Math.Max(medianEdge, lane.m_position - lane.m_width / 2);
                                    medianElevation = Math.Max(medianElevation, lane.m_verticalOffset);
                                }
                                else if (step > 0 && (lane.m_position + lane.m_width / 2) >= laneEdge) {
                                    medianEdge = Math.Min(medianEdge, lane.m_position + lane.m_width / 2);
                                    medianElevation = Math.Max(medianElevation, lane.m_verticalOffset);
                                }
                            }
                        }
                    }

                    if (firstInnerLane >= 0) {
                        // apply AllowServiceLane

                        for (sortedIndex = scanStart; sortedIndex != firstInnerLane; sortedIndex += step) {
                            var laneIndex = m_sortedLanes[sortedIndex];
                            var extLane = m_extLanes[laneIndex];
                            if ((extLane.m_extFlags & (ExtLaneFlags.Outer | workingDirection)) == (ExtLaneFlags.Outer | workingDirection)
                                    && lanes[laneIndex].IsCarLane()) {
                                extLane.m_extFlags |= ExtLaneFlags.AllowServiceLane;
                            }
                        }

                        // apply Inner

                        for (; sortedIndex != scanEnd; sortedIndex += step) {
                            var laneIndex = m_sortedLanes[sortedIndex];
                            var extLane = m_extLanes[laneIndex];
                            if ((extLane.m_extFlags & (ExtLaneFlags.Outer | workingDirection)) == (ExtLaneFlags.Outer | workingDirection)) {
                                extLane.m_extFlags &= ~ExtLaneFlags.Outer;
                                extLane.m_extFlags |= ExtLaneFlags.Inner;
                                var lane = lanes[laneIndex];
                                if (lane.m_allowConnect)
                                    extLane.m_extFlags |= ExtLaneFlags.ForbidControlledLanes;
                                else if (lane.IsCarLane())
                                    extLane.m_extFlags |= ExtLaneFlags.AllowExpressLane;
                            }
                        }
                    }

                    workingDirection = ExtLaneFlags.BackwardGroup;
                    oppositeDirection = ExtLaneFlags.ForwardGroup;
                    scanStart = lastDisplacedOuterForward + 1;
                    scanEnd = lastDisplacedOuterBackward;
                    step = +1;
                }
            }

            m_laneGroups = Enumerable.Range(0, lanes.Length)
                            .Select(index => new { index, lane = lanes[index], extLane = m_extLanes[index] })
                            .GroupBy(l => l.extLane.m_extFlags & LaneGroupingKey)
                            .Where(g => g.Key != 0)
                            .Select(g => new LaneGroupInfo {
                                m_extLaneFlags = g.Select(l => l.extLane.m_extFlags).Aggregate((ExtLaneFlags x, ExtLaneFlags y) => x | y),
                                m_sortedLanes = g.OrderBy(g => g.lane.m_position).Select(g => g.index).ToArray(),
                            })
                            .OrderBy(lg => lanes[lg.m_sortedLanes[0]].m_position)
                            .ToArray();

            m_forwardExtLaneFlags = m_extLanes.Select(l => l.m_extFlags).Where(f => (f & ExtLaneFlags.ForwardGroup) != 0).DefaultIfEmpty().Aggregate((x, y) => x | y);
            m_backwardExtLaneFlags = m_extLanes.Select(l => l.m_extFlags).Where(f => (f & ExtLaneFlags.BackwardGroup) != 0).DefaultIfEmpty().Aggregate((x, y) => x | y);

            m_extLaneFlags = m_forwardExtLaneFlags | m_backwardExtLaneFlags;
        }

        internal class ExtLaneInfo {

            public ExtLaneFlags m_extFlags;

            public ExtLaneInfo(NetInfo.Lane lane) {
                if (lane.IsRoadLane()) {

                    switch (lane.m_direction) {
                        case NetInfo.Direction.Forward:
                        case NetInfo.Direction.AvoidBackward:
                            m_extFlags |= ExtLaneFlags.ForwardGroup;
                            break;

                        case NetInfo.Direction.Backward:
                        case NetInfo.Direction.AvoidForward:
                            m_extFlags |= ExtLaneFlags.BackwardGroup;
                            break;

                        case NetInfo.Direction.Both:
                        case NetInfo.Direction.AvoidBoth:
                            m_extFlags |= ExtLaneFlags.ForwardGroup | ExtLaneFlags.BackwardGroup;
                            break;
                    }
                }
            }
        }

        internal class LaneGroupInfo {

            /// <summary>
            /// Indices of the lanes in this group, sorted by position.
            /// </summary>
            public int[] m_sortedLanes;

            /// <summary>
            /// Aggregate lane flags.
            /// </summary>
            public ExtLaneFlags m_extLaneFlags;
        }
    }
}
