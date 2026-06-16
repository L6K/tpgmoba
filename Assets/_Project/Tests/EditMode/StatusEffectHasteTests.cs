using NUnit.Framework;
using Enigma.Combat;

namespace Enigma.Tests
{
    public sealed class StatusEffectHasteTests
    {
        [Test]
        public void ApplyHaste_RaisesMoveSpeedAboveOne()
        {
            var m = new StatusEffectModel();
            m.ApplyHaste(0.25f, 5f);
            Assert.AreEqual(1.25f, m.MoveSpeedMultiplier, 0.001f);
            Assert.IsTrue(m.IsHasted);
        }

        [Test]
        public void HasteAndSlow_Multiply()
        {
            var m = new StatusEffectModel();
            m.ApplySlow(0.5f, 5f);   // ×0.5
            m.ApplyHaste(0.2f, 5f);  // ×1.2
            Assert.AreEqual(0.6f, m.MoveSpeedMultiplier, 0.001f);
        }

        [Test]
        public void Haste_ExpiresAfterDuration()
        {
            var m = new StatusEffectModel();
            m.ApplyHaste(0.3f, 1f);
            m.Tick(1.1f);
            Assert.IsFalse(m.IsHasted);
            Assert.AreEqual(1f, m.MoveSpeedMultiplier, 0.001f);
        }

        [Test]
        public void StrongestHaste_Wins()
        {
            var m = new StatusEffectModel();
            m.ApplyHaste(0.1f, 5f);
            m.ApplyHaste(0.4f, 5f);
            Assert.AreEqual(1.4f, m.MoveSpeedMultiplier, 0.001f);
        }

        [Test]
        public void Clear_RemovesHaste()
        {
            var m = new StatusEffectModel();
            m.ApplyHaste(0.3f, 5f);
            m.Clear();
            Assert.IsFalse(m.IsHasted);
            Assert.AreEqual(1f, m.MoveSpeedMultiplier, 0.001f);
        }
    }
}
