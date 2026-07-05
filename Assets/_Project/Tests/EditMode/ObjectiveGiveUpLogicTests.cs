using NUnit.Framework;
using Enigma.Character;

namespace Enigma.Tests
{
    public sealed class ObjectiveGiveUpLogicTests
    {
        [Test]
        public void NextStuckSince_InRange_ResetsToNaN()
        {
            float result = ObjectiveGiveUpLogic.NextStuckSince(
                currentStuckSince: 5f, inEngageRange: true, now: 10f);
            Assert.IsTrue(float.IsNaN(result));
        }

        [Test]
        public void NextStuckSince_OutOfRange_FirstFrame_StartsNow()
        {
            float result = ObjectiveGiveUpLogic.NextStuckSince(
                currentStuckSince: float.NaN, inEngageRange: false, now: 10f);
            Assert.AreEqual(10f, result);
        }

        [Test]
        public void NextStuckSince_OutOfRange_KeepsOriginalStartTime()
        {
            float result = ObjectiveGiveUpLogic.NextStuckSince(
                currentStuckSince: 10f, inEngageRange: false, now: 25f);
            Assert.AreEqual(10f, result);
        }

        [Test]
        public void ShouldGiveUp_NotStuck_ReturnsFalse()
        {
            Assert.IsFalse(ObjectiveGiveUpLogic.ShouldGiveUp(float.NaN, now: 100f));
        }

        [Test]
        public void ShouldGiveUp_BelowTimeout_ReturnsFalse()
        {
            bool result = ObjectiveGiveUpLogic.ShouldGiveUp(
                stuckSince: 10f, now: 10f + ObjectiveGiveUpLogic.StuckTimeout - 0.1f);
            Assert.IsFalse(result);
        }

        [Test]
        public void ShouldGiveUp_AtTimeout_ReturnsTrue()
        {
            bool result = ObjectiveGiveUpLogic.ShouldGiveUp(
                stuckSince: 10f, now: 10f + ObjectiveGiveUpLogic.StuckTimeout);
            Assert.IsTrue(result);
        }

        [Test]
        public void IsOnCooldown_NeverGaveUp_ReturnsFalse()
        {
            Assert.IsFalse(ObjectiveGiveUpLogic.IsOnCooldown(float.NaN, now: 100f));
        }

        [Test]
        public void IsOnCooldown_JustGaveUp_ReturnsTrue()
        {
            bool result = ObjectiveGiveUpLogic.IsOnCooldown(giveUpAt: 50f, now: 50f);
            Assert.IsTrue(result);
        }

        [Test]
        public void IsOnCooldown_BeforeCooldownExpires_ReturnsTrue()
        {
            bool result = ObjectiveGiveUpLogic.IsOnCooldown(
                giveUpAt: 50f, now: 50f + ObjectiveGiveUpLogic.GiveUpCooldown - 0.1f);
            Assert.IsTrue(result);
        }

        [Test]
        public void IsOnCooldown_AfterCooldownExpires_ReturnsFalse()
        {
            bool result = ObjectiveGiveUpLogic.IsOnCooldown(
                giveUpAt: 50f, now: 50f + ObjectiveGiveUpLogic.GiveUpCooldown);
            Assert.IsFalse(result);
        }
    }
}
