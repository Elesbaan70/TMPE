namespace TrafficManager.Util {
    using ColossalFramework;
    using CSUtil.Commons;
    using System.Collections.Generic;
    using TrafficManager.API.Manager;
    using TrafficManager.API.Traffic.Data;
    using TrafficManager.API.Traffic.Enums;
    using TrafficManager.Manager.Impl;
    using UnityEngine;
    using TrafficManager.Util.Extensions;
    using TrafficManager.TrafficLight.Impl;
    using TrafficManager.TrafficLight.Model;

    public static class AutoTimedTrafficLights {
        /// <summary>
        /// allocate dedicated turning lanes.
        /// </summary>
        private static readonly bool SeparateLanes = true;

        /// <summary>
        /// allow cars to take the short turn whenever there is the opportunity. LHT is opposite
        /// </summary>
        private static readonly bool AllowShortTurns = true;

        /// <summary>
        /// Due to game limitations, sometimes allowing short turn can lead to car collisions, unless if
        /// timed traffic lights interface changes. should we currently do not setup lane connector. Should we
        /// allow short turns in such situations anyways?
        /// </summary>
        private static readonly bool AllowCollidingShortTurns = false;

        //Shortcuts:
        private static TrafficLightSimulationManager tlsMan = TrafficLightSimulationManager.Instance;
        private static CustomSegmentLightsManager customTrafficLightsManager = CustomSegmentLightsManager.Instance;
        private static IExtSegmentManager segMan = Constants.ManagerFactory.ExtSegmentManager;
        private static IExtSegmentEndManager segEndMan = Constants.ManagerFactory.ExtSegmentEndManager;
        private static ref TrafficLightSimulation Sim(ushort nodeId) => ref tlsMan.TrafficLightSimulations[nodeId];
        private static ref TimedTrafficLights TimedLight(ushort nodeId) => ref Sim(nodeId).timedLight;

        /// <summary>
        /// The directions toward which the traffic light is green
        /// </summary>
        private enum GreenDir {
            AllRed,
            AllGreen,
            ShortOnly,
        }

        public enum ErrorResult {
            Success = 0,
            NoJunction,
            NotSupported,
            TTLExists,
            Other,
        }

        /// <summary>
        /// creats a sorted list of segmetns connected to nodeId.
        /// roads without outgoing lanes are excluded as they do not need traffic lights
        /// the segments are arranged in a clockwise direction (Counter clock wise for LHT).
        /// </summary>
        /// <param name="nodeId">the junction</param>
        /// <returns>a list of segments aranged in counter clockwise direction.</returns>
        private static List<ushort> ArrangedSegments(ushort nodeId) {
            ClockDirection clockDirection = Shortcuts.LHT
                ? ClockDirection.CounterClockwise
                : ClockDirection.Clockwise;

            List<ushort> segList = new List<ushort>();

            ExtNodeManager extNodeManager = ExtNodeManager.Instance;
            foreach (var segmentId in extNodeManager.GetNodeSegmentIds(nodeId, clockDirection)) {
                if (CountOutgoingLanes(segmentId, nodeId) > 0) {
                    segList.Add(segmentId);
                }
            }

            return segList;
        }

        /// <summary>
        /// Adds an empty timed traffic light if it does not already exists.
        /// additionally allocates dedicated turning lanes if possible.
        /// </summary>
        /// <param name="nodeId">the junction for which we want a traffic light</param>
        /// <returns>true if sucessful</returns>
        public static bool Add(ushort nodeId) {
            List<ushort> nodeGroup = new List<ushort>(1);
            nodeGroup.Add(nodeId);
            return tlsMan.SetUpTimedTrafficLight(nodeId, nodeGroup);
        }

        /// <summary>
        /// Creates and configures default traffic the input junction
        /// </summary>
        /// <param name="nodeId">input junction</param>
        /// <returns>true if successful</returns>
        public static ErrorResult Setup(ushort nodeId) {
            if(tlsMan.HasTimedSimulation(nodeId)) {
                return ErrorResult.TTLExists;
            }

            // issue #575: Support level crossings.
            NetNode.Flags flags = nodeId.ToNode().m_flags;
            if((flags & NetNode.Flags.LevelCrossing) != 0) {
                return ErrorResult.NotSupported;
            }

            var segList = ArrangedSegments(nodeId);
            int n = segList.Count;

            if (n < 3) {
                return ErrorResult.NotSupported;
            }

            if (!Add(nodeId)) {
                return ErrorResult.Other;
            }

            if (SeparateLanes) {
                SeparateTurningLanesUtil.SeparateNode(nodeId, out _, false);
            }

            //Is it special case:
            {
                var segList2Way = TwoWayRoads(segList, out int n2);
                if (n2 < 2) {
                    return ErrorResult.NotSupported;
                }
                bool b = HasIncommingOneWaySegment(nodeId);
                if (n2 == 2 && !b) {
                    return SetupSpecial(nodeId, segList2Way);
                }
            }

            for (int i = 0; i < n; ++i) {
                TimedTrafficLightsStep step = TimedLight(nodeId).AddStep(
                    minTime: 3,
                    maxTime: 8,
                    changeMetric: StepChangeMetric.Default,
                    waitFlowBalance: 0.3f,
                    makeRed:true);

                SetupHelper(step, nodeId, segList[i], GreenDir.AllGreen);

                ushort nextSegmentId = segList[(i + 1) % n];
                if ( NeedsShortOnly(nextSegmentId, nodeId)) {
                    SetupHelper(step, nodeId, nextSegmentId, GreenDir.ShortOnly);
                } else {
                    SetupHelper(step, nodeId, nextSegmentId, GreenDir.AllRed);
                }
                for (int j = 2; j < n; ++j) {
                    SetupHelper(step, nodeId, segList[(i + j) % n], GreenDir.AllRed);
                }
            }

            Sim(nodeId).Housekeeping();
            TimedLight(nodeId).Start();
            return ErrorResult.Success;
        }

        /// <summary>
        /// speical case where:
        /// multiple outgoing one way roads. only two 2way roads.
        ///  - each 1-way road gets a go
        ///  - then the two 2-way roads get a go.
        /// this way we can save one step.
        /// </summary>
        /// <param name="nodeId"></param>
        /// <param name="segList2Way"></param>
        /// <returns></returns>
        private static ErrorResult SetupSpecial(ushort nodeId, List<ushort> segList2Way) {
            var segList1Way = OneWayRoads(nodeId, out var n1);

            // the two 2-way roads get a go.
            {
                TimedTrafficLightsStep step = TimedLight(nodeId).AddStep(
                    minTime: 3,
                    maxTime: 8,
                    changeMetric: StepChangeMetric.Default,
                    waitFlowBalance: 0.3f,
                    makeRed: true);

                SetupHelper(step, nodeId, segList2Way[0], GreenDir.AllGreen);
                SetupHelper(step, nodeId, segList2Way[1], GreenDir.AllGreen);
                foreach (var segId in segList1Way) {
                    SetupHelper(step, nodeId, segId, GreenDir.AllRed);
                }
            }

            //each 1-way road gets a go
            for (int i = 0; i < n1; ++i) {
                TimedTrafficLightsStep step = TimedLight(nodeId).AddStep(
                    minTime: 3,
                    maxTime: 8,
                    changeMetric: StepChangeMetric.Default,
                    waitFlowBalance: 0.3f,
                    makeRed: true);

                SetupHelper(step, nodeId, segList1Way[i], GreenDir.AllGreen);
                for (int j = 1; j < n1; ++j) {
                    SetupHelper(step, nodeId, segList1Way[(i + j) % n1], GreenDir.AllRed);
                }
                foreach (var segId in segList2Way) {
                    SetupHelper(step, nodeId, segId, GreenDir.AllRed);
                }
            }

            Sim(nodeId).Housekeeping();
            TimedLight(nodeId).Start();
            return ErrorResult.Success;
        }

        /// <summary>
        /// Configures traffic light for and for all lane types at input segmentId, nodeId, and step.
        /// </summary>
        /// <param name="step"></param>
        /// <param name="nodeId"></param>
        /// <param name="segmentId"></param>
        /// <param name="m">Determines which directions are green</param>
        private static void SetupHelper(TimedTrafficLightsStep step, ushort nodeId, ushort segmentId, GreenDir m) {
            bool startNode = segmentId.ToSegment().IsStartNode(nodeId);
            ref NetNode netNode = ref nodeId.ToNode();

            //get step data for side seg
            CustomSegmentLights liveSegmentLights = customTrafficLightsManager.GetSegmentLights(segmentId, startNode);

            //for each lane type
            foreach (SegmentLightGroup group in liveSegmentLights.Groups) {
                //set light mode
                CustomSegmentLight liveSegmentLight = liveSegmentLights.GetCustomLight(group);
                liveSegmentLight.CurrentMode = LightMode.All;

                TimedLight(nodeId).ChangeLightMode(
                    segmentId,
                    group,
                    liveSegmentLight.CurrentMode);

                // set light states
                var green = RoadBaseAI.TrafficLightState.Green;
                var red = RoadBaseAI.TrafficLightState.Red;
                switch (m) {
                    case GreenDir.AllRed:
                        liveSegmentLight.SetStates(red, red, red);
                        break;

                    case GreenDir.AllGreen:
                        liveSegmentLight.SetStates(green, green, green);
                        break;

                    case GreenDir.ShortOnly: {
                            // calculate directions
                            ref ExtSegmentEnd segEnd = ref segEndMan.ExtSegmentEnds[segEndMan.GetIndex(segmentId, nodeId)];
                            segEndMan.CalculateOutgoingLeftStraightRightSegments(ref segEnd, ref netNode, out bool bLeft, out bool bForward, out bool bRight);
                            bool lht = Shortcuts.LHT;
                            bool bShort = lht ? bLeft : bRight;
                            bool bLong = lht ? bRight : bLeft;

                            if (bShort) {
                                SetStates(liveSegmentLight, red, red, green);
                            } else if (bLong) {
                                // go forward instead of short
                                SetStates(liveSegmentLight, green, red, red);
                            } else {
                                Debug.LogAssertion("Unreachable code.");
                                liveSegmentLight.SetStates(green, green, green);
                            }
                            break;
                        }
                    default:
                        Debug.LogAssertion("Unreachable code.");
                        liveSegmentLight.SetStates(green, green, green);
                        break;
                } // end switch
            } // end foreach
            step.UpdateLights(); //save
        }

        /// <summary>
        /// converst forward, short-turn and far-turn to mainLight, leftLigh, rightLight respectively according to
        /// whether the traffic is RHT or LHT
        /// </summary>
        private static void SetStates(
            CustomSegmentLight liveSegmentLight,
            RoadBaseAI.TrafficLightState sForard,
            RoadBaseAI.TrafficLightState sFar,
            RoadBaseAI.TrafficLightState sShort) {
            if (Shortcuts.LHT) {
                liveSegmentLight.SetStates(mainLight: sForard, leftLight: sShort, rightLight: sFar);
            } else {
                liveSegmentLight.SetStates(mainLight: sForard, leftLight: sFar, rightLight: sShort);
            }
        }

        private static bool HasIncommingOneWaySegment(ushort nodeId) {
            ref NetNode node = ref nodeId.ToNode();
            for (int i = 0; i < 8; ++i) {
                var segId = node.GetSegment(i);
                if (segId != 0 && segMan.CalculateIsOneWay(segId)) {
                    int n = CountIncomingLanes(segId, nodeId);
                    int dummy = CountOutgoingLanes(segId, nodeId);
                    if (n > 0) {
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>filters out oneway roads from segList. Assumes all segments in seglist have outgoing lanes. </summary>
        /// <param name="segList"> List of segments returned by SWSegments. </param>
        /// <param name="count">number of two way roads connected to the junction</param>
        /// <returns>A list of all two way roads connected to the junction</returns>
        private static List<ushort> TwoWayRoads(List<ushort> segList, out int count) {
            List<ushort> segList2 = new List<ushort>();
            foreach(var segId in segList) {
                if (!segMan.CalculateIsOneWay(segId)) {
                    segList2.Add(segId);
                }
            }
            count = segList2.Count;
            return segList2;
        }

        /// <param name="nodeId"></param>
        /// <param name="count">number of all one way roads at input junction.</param>
        /// <returns>list of all oneway roads connected to input junction</returns>
        private static List<ushort> OneWayRoads(ushort nodeId, out int count) {
            List<ushort> segList2 = new List<ushort>();
            ref NetNode node = ref nodeId.ToNode();
            for (int i = 0; i < 8; ++i) {
                var segId = node.GetSegment(i);
                if (segMan.CalculateIsOneWay(segId)) {
                    segList2.Add(segId);
                }
            }
            count = segList2.Count;
            return segList2;
        }

        /// <summary>
        /// wierd road connections where short turn is possible only  if:
        ///  - timed traffic light gives more control over directions to turn to.
        ///  - lane connector is used.
        ///  Note: for other more normal cases furthur chaking is performed in SetupHelper() to determine if a short turn is necessary.
        /// </summary>
        /// <param name="segmentId"></param>
        /// <param name="nodeId"></param>
        /// <returns>
        /// false AllowShortTurns == false
        /// otherwise true if AllowCollidingShortTurns == true
        /// otherwise false if it is special case described above.
        /// otherwise true if short turn is easily possible, without complications.
        /// otherwise false.
        /// </returns>
        private static bool NeedsShortOnly(ushort segmentId, ushort nodeId) {
            if (!AllowShortTurns) {
                return false;
            }

            if (AllowCollidingShortTurns) {
                return true;
            }

            ref NetSegment netSegment = ref segmentId.ToSegment();
            bool lht = Shortcuts.LHT;
            ArrowDirection shortDir = lht ? ArrowDirection.Left : ArrowDirection.Right;
            int nShort = CountDirSegs(segmentId, nodeId, shortDir);

            if (nShort > 1) {
                return false;
            }
            if (nShort == 1) {
                ushort nextSegmentId = lht
                    ? netSegment.GetLeftSegment(nodeId)
                    : netSegment.GetRightSegment(nodeId);
                return !segMan.CalculateIsOneWay(nextSegmentId);
            }
            int nForward = CountDirSegs(segmentId, nodeId, ArrowDirection.Forward);
            if (nForward > 1) {
                return false;
            }
            if (nForward == 1) {
                // RHT: if there are not segments to the right GetRightSegment() returns the forward segment.
                // LHT: if there are not segments to the left GetLeftSegment() returns the forward segment.
                ushort nextSegmentId = lht ? netSegment.GetLeftSegment(nodeId) : netSegment.GetRightSegment(nodeId);
                return !segMan.CalculateIsOneWay(nextSegmentId);
            }
            return false;
        }

        /// <summary>
        /// Count number of applicable lanes entering or leaving a segment via specific node.
        /// </summary>
        /// <param name="segmentId">The id of the segment to inspect.</param>
        /// <param name="nodeId">The id of node where lanes should be counted.</param>
        /// <param name="incoming">
        /// If <c>true</c>, count lanes entering the segment from the junction.
        /// If <c>false</c>, count lanes going from the segment to the junction.
        /// </param>
        /// <returns>Returns number of lanes matching the specified criteria.</returns>
        private static int CountLanes(ushort segmentId, ushort nodeId, bool incoming) =>
            segmentId.ToSegment().CountLanes(
                nodeId,
                LaneArrowManager.LANE_TYPES,
                LaneArrowManager.VEHICLE_TYPES,
                incoming);

        private static int CountOutgoingLanes(ushort segmentId, ushort nodeId)
            => CountLanes(segmentId, nodeId, false);
        private static int CountIncomingLanes(ushort segmentId, ushort nodeId)
            => CountLanes(segmentId, nodeId, true);

        /// <summary>
        /// Counts the number of roads toward the given directions.
        /// </summary>
        /// <param name="segmentId"></param>
        /// <param name="nodeId"></param>
        /// <param name="dir"></param>
        /// <returns></returns>
        private static int CountDirSegs(ushort segmentId, ushort nodeId, ArrowDirection dir) {
            ExtSegmentEnd segEnd = segEndMan.ExtSegmentEnds[segEndMan.GetIndex(segmentId, nodeId)];
            int ret = 0;

            ref NetNode node = ref nodeId.ToNode();
            for (int i = 0; i < 8; ++i) {
                ushort segId = node.GetSegment(i);
                if (segId != 0) {
                    if (segEndMan.GetDirection(ref segEnd, segId) == dir) {
                        ret++;
                    }
                }
            }

            return ret;
        }
    }
}
