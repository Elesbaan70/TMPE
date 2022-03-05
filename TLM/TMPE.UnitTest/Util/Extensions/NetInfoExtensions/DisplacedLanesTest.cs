using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using TrafficManager.Util.Extensions;
using static NetInfo.Direction;
using static TMUnitTest.Util.Extensions.NetInfoExtensions.NetInfoTestUtil;

namespace TMUnitTest.Util.Extensions.NetInfoExtensions {
    [TestClass]
    public class DisplacedLanesTest {
        [TestMethod]
        public void TestBF() {
            var lanes = new[] {
                Lane(Backward, -1),
                Lane(Forward, 1),
            };

            Assert.IsFalse(lanes[0].IsDisplaced(lanes));
            Assert.IsFalse(lanes[1].IsDisplaced(lanes));

            Assert.IsFalse(lanes.IsAnyDisplaced());
        }

        [TestMethod]
        public void TestFB() {
            var lanes = new[] {
                Lane(Forward, -1),
                Lane(Backward, 1),
            };

            Assert.IsTrue(lanes[0].IsDisplaced(lanes));
            Assert.IsTrue(lanes[1].IsDisplaced(lanes));

            Assert.IsTrue(lanes.IsAnyDisplaced());
        }

        [TestMethod]
        public void TestBFB() {
            var lanes = new[] {
                Lane(Backward, -1),
                Lane(Forward, 0),
                Lane(Backward, 1),
            };

            Assert.IsFalse(lanes[0].IsDisplaced(lanes));
            Assert.IsTrue(lanes[1].IsDisplaced(lanes));
            Assert.IsTrue(lanes[2].IsDisplaced(lanes));

            Assert.IsTrue(lanes.IsAnyDisplaced());
        }

        [TestMethod]
        public void TestFBF() {
            var lanes = new[] {
                Lane(Forward, -1),
                Lane(Backward, 0),
                Lane(Forward, 1),
            };

            Assert.IsTrue(lanes[0].IsDisplaced(lanes));
            Assert.IsTrue(lanes[1].IsDisplaced(lanes));
            Assert.IsFalse(lanes[2].IsDisplaced(lanes));

            Assert.IsTrue(lanes.IsAnyDisplaced());
        }

        [TestMethod]
        public void TestBFBF() {
            var lanes = new[] {
                Lane(Backward, -2),
                Lane(Forward, -1),
                Lane(Backward, 1),
                Lane(Forward, 2),
            };

            Assert.IsFalse(lanes[0].IsDisplaced(lanes));
            Assert.IsTrue(lanes[1].IsDisplaced(lanes));
            Assert.IsTrue(lanes[2].IsDisplaced(lanes));
            Assert.IsFalse(lanes[3].IsDisplaced(lanes));

            Assert.IsTrue(lanes.IsAnyDisplaced());
        }

        [TestMethod]
        public void TestFBFB() {
            var lanes = new[] {
                Lane(Forward, -2),
                Lane(Backward, -1),
                Lane(Forward, 1),
                Lane(Backward, 2),
            };

            Assert.IsTrue(lanes[0].IsDisplaced(lanes));
            Assert.IsTrue(lanes[1].IsDisplaced(lanes));
            Assert.IsTrue(lanes[2].IsDisplaced(lanes));
            Assert.IsTrue(lanes[3].IsDisplaced(lanes));

            Assert.IsTrue(lanes.IsAnyDisplaced());
        }

        [TestMethod]
        public void TestTwoWayNotDisplaced() {
            var lanes = new[] {
                Lane(Backward, -1),
                Lane(Both, 0),
                Lane(Forward, 1),
            };

            Assert.IsFalse(lanes[0].IsDisplaced(lanes));
            Assert.IsFalse(lanes[1].IsDisplaced(lanes));
            Assert.IsFalse(lanes[2].IsDisplaced(lanes));

            Assert.IsFalse(lanes.IsAnyDisplaced());
        }

        [TestMethod]
        public void TestTwoWayDisplacedLeft() {
            var lanes = new[] {
                Lane(Both, -1),
                Lane(Backward, 0),
                Lane(Forward, 1),
            };

            Assert.IsTrue(lanes[0].IsDisplaced(lanes));
            Assert.IsTrue(lanes[1].IsDisplaced(lanes));
            Assert.IsFalse(lanes[2].IsDisplaced(lanes));

            Assert.IsTrue(lanes.IsAnyDisplaced());
        }

        [TestMethod]
        public void TestTwoWayDisplacedRight() {
            var lanes = new[] {
                Lane(Backward, -1),
                Lane(Forward, 0),
                Lane(Both, 1),
            };

            Assert.IsFalse(lanes[0].IsDisplaced(lanes));
            Assert.IsTrue(lanes[1].IsDisplaced(lanes));
            Assert.IsTrue(lanes[2].IsDisplaced(lanes));

            Assert.IsTrue(lanes.IsAnyDisplaced());
        }
    }
}