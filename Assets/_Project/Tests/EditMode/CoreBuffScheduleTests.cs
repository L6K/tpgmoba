using System.Linq;
using Enigma.GameModes;
using NUnit.Framework;

namespace Enigma.Tests
{
    public sealed class CoreBuffScheduleTests
    {
        private const float Tolerance = 1e-4f;

        [Test]
        public void ForKillCount_One_GrantsDamageOnly()
        {
            var grants = CoreBuffSchedule.ForKillCount(1);

            Assert.AreEqual(1, grants.Count);
            Assert.AreEqual(ObjectiveBuffType.Damage, grants[0].Type);
            Assert.AreEqual(0.20f, grants[0].Magnitude, Tolerance);
            Assert.AreEqual(45f, grants[0].Duration, Tolerance);
        }

        [Test]
        public void ForKillCount_Two_AddsMoveSpeed()
        {
            var grants = CoreBuffSchedule.ForKillCount(2);

            Assert.AreEqual(2, grants.Count);

            var damage = grants.Single(g => g.Type == ObjectiveBuffType.Damage);
            Assert.AreEqual(0.20f, damage.Magnitude, Tolerance);

            var moveSpeed = grants.Single(g => g.Type == ObjectiveBuffType.MoveSpeed);
            Assert.AreEqual(0.15f, moveSpeed.Magnitude, Tolerance);
            Assert.AreEqual(45f, moveSpeed.Duration, Tolerance);
        }

        [Test]
        public void ForKillCount_Three_UpgradesDamageAndAddsShieldAndStructureDamage()
        {
            var grants = CoreBuffSchedule.ForKillCount(3);

            Assert.AreEqual(4, grants.Count);

            var damage = grants.Single(g => g.Type == ObjectiveBuffType.Damage);
            Assert.AreEqual(0.25f, damage.Magnitude, Tolerance);
            Assert.AreEqual(45f, damage.Duration, Tolerance);

            var moveSpeed = grants.Single(g => g.Type == ObjectiveBuffType.MoveSpeed);
            Assert.AreEqual(0.15f, moveSpeed.Magnitude, Tolerance);

            var shield = grants.Single(g => g.Type == ObjectiveBuffType.Shield);
            Assert.AreEqual(150f, shield.Magnitude, Tolerance);
            Assert.AreEqual(10f, shield.Duration, Tolerance);

            var structureDamage = grants.Single(g => g.Type == ObjectiveBuffType.StructureDamage);
            Assert.AreEqual(1.0f, structureDamage.Magnitude, Tolerance);
            Assert.AreEqual(45f, structureDamage.Duration, Tolerance);
        }

        [Test]
        public void ForKillCount_Four_SameAsThree()
        {
            var three = CoreBuffSchedule.ForKillCount(3);
            var four  = CoreBuffSchedule.ForKillCount(4);

            Assert.AreEqual(three.Count, four.Count);

            var damage3 = three.Single(g => g.Type == ObjectiveBuffType.Damage);
            var damage4 = four.Single(g => g.Type == ObjectiveBuffType.Damage);
            Assert.AreEqual(damage3.Magnitude, damage4.Magnitude, Tolerance);

            Assert.IsTrue(four.Any(g => g.Type == ObjectiveBuffType.StructureDamage));
            Assert.IsTrue(four.Any(g => g.Type == ObjectiveBuffType.Shield));
            Assert.IsTrue(four.Any(g => g.Type == ObjectiveBuffType.MoveSpeed));
        }
    }
}
