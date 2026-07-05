using NUnit.Framework;
using Enigma.Minion;

namespace Enigma.Tests
{
    public sealed class JungleLeashLogicTests
    {
        [Test]
        public void ShouldReturn_WithinLeashDistance_ReturnsFalse()
        {
            Assert.IsFalse(JungleLeashLogic.ShouldReturn(JungleLeashLogic.LeashDistance - 0.01f));
        }

        [Test]
        public void ShouldReturn_AtLeashDistance_ReturnsFalse()
        {
            // 境界ちょうどは「超過」ではないため帰還しない
            Assert.IsFalse(JungleLeashLogic.ShouldReturn(JungleLeashLogic.LeashDistance));
        }

        [Test]
        public void ShouldReturn_BeyondLeashDistance_ReturnsTrue()
        {
            Assert.IsTrue(JungleLeashLogic.ShouldReturn(JungleLeashLogic.LeashDistance + 0.01f));
        }

        [Test]
        public void IsReturnComplete_BeyondCompleteDistance_ReturnsFalse()
        {
            Assert.IsFalse(JungleLeashLogic.IsReturnComplete(JungleLeashLogic.ReturnCompleteDistance + 0.01f));
        }

        [Test]
        public void IsReturnComplete_AtCompleteDistance_ReturnsTrue()
        {
            Assert.IsTrue(JungleLeashLogic.IsReturnComplete(JungleLeashLogic.ReturnCompleteDistance));
        }

        [Test]
        public void IsReturnComplete_WithinCompleteDistance_ReturnsTrue()
        {
            Assert.IsTrue(JungleLeashLogic.IsReturnComplete(0f));
        }

        [Test]
        public void LeashDistance_Is15Meters()
        {
            Assert.AreEqual(15f, JungleLeashLogic.LeashDistance);
        }

        [Test]
        public void ReturnCompleteDistance_Is2Meters()
        {
            Assert.AreEqual(2f, JungleLeashLogic.ReturnCompleteDistance);
        }
    }
}
