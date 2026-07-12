using NUnit.Framework;
using Enigma.Minion;

namespace Enigma.Tests
{
    public sealed class MinionSiegeLogicTests
    {
        [Test]
        public void Structure_GetsTripleMultiplier()
        {
            Assert.AreEqual(MinionSiegeLogic.StructureMultiplier, MinionSiegeLogic.Multiplier(true));
            Assert.AreEqual(3.0f, MinionSiegeLogic.Multiplier(true));
        }

        [Test]
        public void NonStructure_NoBonus()
        {
            Assert.AreEqual(1f, MinionSiegeLogic.Multiplier(false));
        }
    }
}
