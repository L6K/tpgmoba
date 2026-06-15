using NUnit.Framework;
using Enigma.Combat;

namespace Enigma.Tests
{
    public sealed class TeamRulesTests
    {
        [Test]
        public void CanDamage_SameTeam_ReturnsFalse()
        {
            Assert.IsFalse(TeamRules.CanDamage(TeamId.Blue, TeamId.Blue));
            Assert.IsFalse(TeamRules.CanDamage(TeamId.Red, TeamId.Red));
        }

        [Test]
        public void CanDamage_EnemyTeam_ReturnsTrue()
        {
            Assert.IsTrue(TeamRules.CanDamage(TeamId.Blue, TeamId.Red));
            Assert.IsTrue(TeamRules.CanDamage(TeamId.Red, TeamId.Blue));
        }

        [Test]
        public void CanDamage_NeutralAttacker_ReturnsTrue()
        {
            Assert.IsTrue(TeamRules.CanDamage(TeamId.Neutral, TeamId.Blue));
            Assert.IsTrue(TeamRules.CanDamage(TeamId.Neutral, TeamId.Red));
        }

        [Test]
        public void CanDamage_NeutralTarget_ReturnsTrue()
        {
            Assert.IsTrue(TeamRules.CanDamage(TeamId.Blue, TeamId.Neutral));
            Assert.IsTrue(TeamRules.CanDamage(TeamId.Red, TeamId.Neutral));
        }

        [Test]
        public void CanDamage_NeutralVsNeutral_ReturnsTrue()
        {
            Assert.IsTrue(TeamRules.CanDamage(TeamId.Neutral, TeamId.Neutral));
        }
    }
}
