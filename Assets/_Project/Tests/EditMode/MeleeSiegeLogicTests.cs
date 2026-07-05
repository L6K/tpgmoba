using NUnit.Framework;
using Enigma.Combat;

namespace Enigma.Tests
{
    public sealed class MeleeSiegeLogicTests
    {
        [Test]
        public void MeleeRange_GetsStructureBonus()
        {
            Assert.AreEqual(MeleeSiegeLogic.StructureMultiplier, MeleeSiegeLogic.Multiplier(3.5f));
            Assert.AreEqual(MeleeSiegeLogic.StructureMultiplier, MeleeSiegeLogic.Multiplier(4f));
        }

        [Test]
        public void ThresholdBoundary_IsMelee()
        {
            Assert.AreEqual(MeleeSiegeLogic.StructureMultiplier,
                MeleeSiegeLogic.Multiplier(MeleeSiegeLogic.MeleeRangeThreshold));
        }

        [Test]
        public void RangedRange_NoBonus()
        {
            Assert.AreEqual(1f, MeleeSiegeLogic.Multiplier(MeleeSiegeLogic.MeleeRangeThreshold + 0.1f));
            Assert.AreEqual(1f, MeleeSiegeLogic.Multiplier(12f));
            Assert.AreEqual(1f, MeleeSiegeLogic.Multiplier(15f));
        }
    }
}
