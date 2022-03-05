using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using TrafficManager.Util.Extensions;
using static TMUnitTest.Util.Extensions.NetInfoExtensions.NetInfoTestUtil;

namespace TMUnitTest.Util.Extensions.NetInfoExtensions {
    [TestClass]
    public class NetInfoExtensionsTest {
        [TestMethod]
        public void TestIsForward() {
            Assert.IsFalse(Lane(NetInfo.Direction.None, 0).IsForward());
            Assert.IsTrue(Lane(NetInfo.Direction.Forward, 0).IsForward());
            Assert.IsFalse(Lane(NetInfo.Direction.Backward, 0).IsForward());
            Assert.IsTrue(Lane(NetInfo.Direction.Both, 0).IsForward());
            Assert.IsFalse(Lane(NetInfo.Direction.Avoid, 0).IsForward());
            Assert.IsTrue(Lane(NetInfo.Direction.AvoidBackward, 0).IsForward());
            Assert.IsFalse(Lane(NetInfo.Direction.AvoidForward, 0).IsForward());
            Assert.IsFalse(Lane(NetInfo.Direction.AvoidBoth, 0).IsForward());
        }

        [TestMethod]
        public void TestIsBackward() {
            Assert.IsFalse(Lane(NetInfo.Direction.None, 0).IsBackward());
            Assert.IsFalse(Lane(NetInfo.Direction.Forward, 0).IsBackward());
            Assert.IsTrue(Lane(NetInfo.Direction.Backward, 0).IsBackward());
            Assert.IsTrue(Lane(NetInfo.Direction.Both, 0).IsBackward());
            Assert.IsFalse(Lane(NetInfo.Direction.Avoid, 0).IsBackward());
            Assert.IsFalse(Lane(NetInfo.Direction.AvoidBackward, 0).IsBackward());
            Assert.IsTrue(Lane(NetInfo.Direction.AvoidForward, 0).IsBackward());
            Assert.IsFalse(Lane(NetInfo.Direction.AvoidBoth, 0).IsBackward());
        }
    }
}