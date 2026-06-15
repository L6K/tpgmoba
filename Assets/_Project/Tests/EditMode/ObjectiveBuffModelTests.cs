using Enigma.Combat;
using Enigma.GameModes;
using NUnit.Framework;

namespace Enigma.Tests
{
    public sealed class ObjectiveBuffModelTests
    {
        private const float Tolerance = 1e-4f;

        [Test]
        public void Grant_ValidBuff_GetMagnitudeReturnsMagnitude()
        {
            var model = new ObjectiveBuffModel();

            model.Grant(TeamId.Blue, ObjectiveBuffType.Damage, 0.15f, 10f, 0f);

            Assert.AreEqual(0.15f, model.GetMagnitude(TeamId.Blue, ObjectiveBuffType.Damage, 5f), Tolerance);
        }

        [Test]
        public void GetMagnitude_AfterExpiry_ReturnsZero()
        {
            var model = new ObjectiveBuffModel();

            model.Grant(TeamId.Blue, ObjectiveBuffType.Damage, 0.15f, 10f, 0f);

            Assert.AreEqual(0f, model.GetMagnitude(TeamId.Blue, ObjectiveBuffType.Damage, 10f), Tolerance);
        }

        [Test]
        public void GetMagnitude_OverlappingSameTeamAndType_ReturnsMaximumMagnitude()
        {
            var model = new ObjectiveBuffModel();

            model.Grant(TeamId.Blue, ObjectiveBuffType.Damage, 0.2f, 20f, 0f);
            model.Grant(TeamId.Blue, ObjectiveBuffType.Damage, 0.5f, 10f, 5f);

            Assert.AreEqual(0.5f, model.GetMagnitude(TeamId.Blue, ObjectiveBuffType.Damage, 6f), Tolerance);
        }

        [Test]
        public void GetMagnitude_StrongerOverlapExpires_ReturnsWeakerStillActiveMagnitude()
        {
            var model = new ObjectiveBuffModel();

            model.Grant(TeamId.Blue, ObjectiveBuffType.Damage, 0.2f, 20f, 0f);
            model.Grant(TeamId.Blue, ObjectiveBuffType.Damage, 0.5f, 10f, 5f);

            Assert.AreEqual(0.2f, model.GetMagnitude(TeamId.Blue, ObjectiveBuffType.Damage, 16f), Tolerance);
        }

        [Test]
        public void Grant_NonPositiveMagnitude_IsIgnored()
        {
            var model = new ObjectiveBuffModel();

            model.Grant(TeamId.Blue, ObjectiveBuffType.Damage, 0f, 10f, 0f);
            model.Grant(TeamId.Blue, ObjectiveBuffType.Damage, -0.1f, 10f, 0f);

            Assert.AreEqual(0f, model.GetMagnitude(TeamId.Blue, ObjectiveBuffType.Damage, 1f), Tolerance);
        }

        [Test]
        public void Grant_NonPositiveDuration_IsIgnored()
        {
            var model = new ObjectiveBuffModel();

            model.Grant(TeamId.Blue, ObjectiveBuffType.Damage, 0.15f, 0f, 0f);
            model.Grant(TeamId.Blue, ObjectiveBuffType.Damage, 0.15f, -1f, 0f);

            Assert.AreEqual(0f, model.GetMagnitude(TeamId.Blue, ObjectiveBuffType.Damage, 0f), Tolerance);
        }

        [Test]
        public void GetMagnitude_DifferentTeam_IsIndependent()
        {
            var model = new ObjectiveBuffModel();

            model.Grant(TeamId.Blue, ObjectiveBuffType.Damage, 0.15f, 10f, 0f);

            Assert.AreEqual(0f, model.GetMagnitude(TeamId.Red, ObjectiveBuffType.Damage, 5f), Tolerance);
        }

        [Test]
        public void GetMagnitude_DifferentType_IsIndependent()
        {
            var model = new ObjectiveBuffModel();

            model.Grant(TeamId.Blue, ObjectiveBuffType.Damage, 0.15f, 10f, 0f);

            Assert.AreEqual(0f, model.GetMagnitude(TeamId.Blue, ObjectiveBuffType.MoveSpeed, 5f), Tolerance);
        }

        [Test]
        public void GetRemainingSeconds_ActiveBuff_ReturnsRemainingTime()
        {
            var model = new ObjectiveBuffModel();

            model.Grant(TeamId.Blue, ObjectiveBuffType.MoveSpeed, 0.1f, 12f, 3f);

            Assert.AreEqual(7f, model.GetRemainingSeconds(TeamId.Blue, ObjectiveBuffType.MoveSpeed, 8f), Tolerance);
        }

        [Test]
        public void GetRemainingSeconds_AfterExpiry_ReturnsZero()
        {
            var model = new ObjectiveBuffModel();

            model.Grant(TeamId.Blue, ObjectiveBuffType.MoveSpeed, 0.1f, 12f, 3f);

            Assert.AreEqual(0f, model.GetRemainingSeconds(TeamId.Blue, ObjectiveBuffType.MoveSpeed, 15f), Tolerance);
        }

        [Test]
        public void GetRemainingSeconds_WhenNowPastExpiry_NeverReturnsNegative()
        {
            var model = new ObjectiveBuffModel();

            model.Grant(TeamId.Blue, ObjectiveBuffType.MoveSpeed, 0.1f, 12f, 3f);

            Assert.AreEqual(0f, model.GetRemainingSeconds(TeamId.Blue, ObjectiveBuffType.MoveSpeed, 100f), Tolerance);
        }

        [Test]
        public void GetRemainingSeconds_OverlappingBuffs_ReturnsLatestExpiryRemainingTime()
        {
            var model = new ObjectiveBuffModel();

            model.Grant(TeamId.Blue, ObjectiveBuffType.Shield, 0.2f, 5f, 0f);
            model.Grant(TeamId.Blue, ObjectiveBuffType.Shield, 0.1f, 20f, 2f);

            Assert.AreEqual(17f, model.GetRemainingSeconds(TeamId.Blue, ObjectiveBuffType.Shield, 5f), Tolerance);
        }

        [Test]
        public void GetActiveTypes_ReturnsOnlyActiveTypes()
        {
            var model = new ObjectiveBuffModel();

            model.Grant(TeamId.Blue, ObjectiveBuffType.Damage, 0.15f, 10f, 0f);
            model.Grant(TeamId.Blue, ObjectiveBuffType.MinionPower, 0.25f, 3f, 0f);

            var types = model.GetActiveTypes(TeamId.Blue, 5f);

            Assert.AreEqual(1, types.Count);
            CollectionAssert.Contains(types, ObjectiveBuffType.Damage);
        }

        [Test]
        public void GetActiveTypes_OverlappingSameType_ReturnsTypeOnlyOnce()
        {
            var model = new ObjectiveBuffModel();

            model.Grant(TeamId.Blue, ObjectiveBuffType.TowerWeaken, 0.1f, 10f, 0f);
            model.Grant(TeamId.Blue, ObjectiveBuffType.TowerWeaken, 0.2f, 20f, 1f);

            var types = model.GetActiveTypes(TeamId.Blue, 2f);

            Assert.AreEqual(1, types.Count);
            CollectionAssert.Contains(types, ObjectiveBuffType.TowerWeaken);
        }

        [Test]
        public void GetActiveTypes_DifferentTeam_IsIndependent()
        {
            var model = new ObjectiveBuffModel();

            model.Grant(TeamId.Blue, ObjectiveBuffType.Damage, 0.15f, 10f, 0f);

            Assert.AreEqual(0, model.GetActiveTypes(TeamId.Red, 5f).Count);
        }

        [Test]
        public void Clear_RemovesAllBuffs()
        {
            var model = new ObjectiveBuffModel();
            model.Grant(TeamId.Blue, ObjectiveBuffType.Damage, 0.15f, 10f, 0f);
            model.Grant(TeamId.Red, ObjectiveBuffType.Shield, 0.3f, 10f, 0f);

            model.Clear();

            Assert.AreEqual(0f, model.GetMagnitude(TeamId.Blue, ObjectiveBuffType.Damage, 5f), Tolerance);
            Assert.AreEqual(0f, model.GetRemainingSeconds(TeamId.Red, ObjectiveBuffType.Shield, 5f), Tolerance);
            Assert.AreEqual(0, model.GetActiveTypes(TeamId.Blue, 5f).Count);
        }

        [Test]
        public void Grant_RemovesExpiredEntriesForSameTeamAndType()
        {
            var model = new ObjectiveBuffModel();

            model.Grant(TeamId.Blue, ObjectiveBuffType.Damage, 0.5f, 5f, 0f);
            model.Grant(TeamId.Blue, ObjectiveBuffType.Damage, 0.2f, 10f, 6f);

            Assert.AreEqual(0.2f, model.GetMagnitude(TeamId.Blue, ObjectiveBuffType.Damage, 6f), Tolerance);
            Assert.AreEqual(10f, model.GetRemainingSeconds(TeamId.Blue, ObjectiveBuffType.Damage, 6f), Tolerance);
        }
    }
}
