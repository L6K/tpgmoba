using Enigma.GameModes;
using NUnit.Framework;

namespace Enigma.Tests
{
    public sealed class PingCommandModelTests
    {
        private const float Tolerance = 1e-4f;

        [Test]
        public void TryIssue_FirstIssue_AddsActivePing()
        {
            var model = new PingCommandModel();

            bool issued = model.TryIssue(PingType.Danger, 12f, -3f, 5f);

            Assert.IsTrue(issued);
            Assert.AreEqual(1, model.ActivePings.Count);
            Assert.AreEqual(PingType.Danger, model.ActivePings[0].Type);
            Assert.AreEqual(12f, model.ActivePings[0].X, Tolerance);
            Assert.AreEqual(-3f, model.ActivePings[0].Z, Tolerance);
            Assert.AreEqual(9f, model.ActivePings[0].ExpiresAt, Tolerance);
        }

        [Test]
        public void TryIssue_BeforeMinInterval_ReturnsFalseAndDoesNotAdd()
        {
            var model = new PingCommandModel(1f, 4f);

            Assert.IsTrue(model.TryIssue(PingType.Danger, 0f, 0f, 10f));
            bool issued = model.TryIssue(PingType.Attack, 1f, 1f, 10.5f);

            Assert.IsFalse(issued);
            Assert.AreEqual(1, model.ActivePings.Count);
        }

        [Test]
        public void TryIssue_AtMinInterval_AddsSecondPing()
        {
            var model = new PingCommandModel(1f, 4f);

            Assert.IsTrue(model.TryIssue(PingType.Danger, 0f, 0f, 10f));
            bool issued = model.TryIssue(PingType.OnMyWay, 2f, 3f, 11f);

            Assert.IsTrue(issued);
            Assert.AreEqual(2, model.ActivePings.Count);
            Assert.AreEqual(PingType.OnMyWay, model.ActivePings[1].Type);
            Assert.AreEqual(15f, model.ActivePings[1].ExpiresAt, Tolerance);
        }

        [Test]
        public void TryIssue_BeforeMinInterval_DoesNotUpdateLastIssuedTime()
        {
            var model = new PingCommandModel(1f, 4f);

            Assert.IsTrue(model.TryIssue(PingType.Danger, 0f, 0f, 10f));
            Assert.IsFalse(model.TryIssue(PingType.Attack, 1f, 1f, 10.5f));
            bool issued = model.TryIssue(PingType.OnMyWay, 2f, 2f, 11f);

            Assert.IsTrue(issued);
            Assert.AreEqual(2, model.ActivePings.Count);
        }

        [Test]
        public void Tick_BeforeExpiry_KeepsPing()
        {
            var model = new PingCommandModel(0f, 4f);
            model.TryIssue(PingType.Danger, 0f, 0f, 2f);

            model.Tick(5.999f);

            Assert.AreEqual(1, model.ActivePings.Count);
        }

        [Test]
        public void Tick_AtExpiry_RemovesPing()
        {
            var model = new PingCommandModel(0f, 4f);
            model.TryIssue(PingType.Danger, 0f, 0f, 2f);

            model.Tick(6f);

            Assert.AreEqual(0, model.ActivePings.Count);
        }

        [Test]
        public void Tick_MultiplePings_RemovesOnlyExpired()
        {
            var model = new PingCommandModel(0f, 4f);
            model.TryIssue(PingType.Danger, 0f, 0f, 0f);
            model.TryIssue(PingType.Attack, 1f, 1f, 2f);

            model.Tick(4f);

            Assert.AreEqual(1, model.ActivePings.Count);
            Assert.AreEqual(PingType.Attack, model.ActivePings[0].Type);
        }

        [Test]
        public void Clear_RemovesAllActivePings()
        {
            var model = new PingCommandModel(0f, 4f);
            model.TryIssue(PingType.Danger, 0f, 0f, 0f);
            model.TryIssue(PingType.Attack, 1f, 1f, 0f);

            model.Clear();

            Assert.AreEqual(0, model.ActivePings.Count);
        }

        [Test]
        public void Clear_DoesNotResetRateLimit()
        {
            var model = new PingCommandModel(1f, 4f);
            model.TryIssue(PingType.Danger, 0f, 0f, 10f);

            model.Clear();
            bool issued = model.TryIssue(PingType.Attack, 1f, 1f, 10.5f);

            Assert.IsFalse(issued);
            Assert.AreEqual(0, model.ActivePings.Count);
        }

        [Test]
        public void SelectByAngle_AtCenters_ReturnsNearestPingType()
        {
            Assert.AreEqual(PingType.Danger, PingCommandModel.SelectByAngle(0f));
            Assert.AreEqual(PingType.OnMyWay, PingCommandModel.SelectByAngle(120f));
            Assert.AreEqual(PingType.Attack, PingCommandModel.SelectByAngle(240f));
        }

        [Test]
        public void SelectByAngle_AtBoundaries_UsesRightSideSector()
        {
            Assert.AreEqual(PingType.OnMyWay, PingCommandModel.SelectByAngle(60f));
            Assert.AreEqual(PingType.Attack, PingCommandModel.SelectByAngle(180f));
            Assert.AreEqual(PingType.Danger, PingCommandModel.SelectByAngle(300f));
        }

        [Test]
        public void SelectByAngle_NegativeAngle_NormalizesIntoDanger()
        {
            Assert.AreEqual(PingType.Danger, PingCommandModel.SelectByAngle(-30f));
        }

        [Test]
        public void SelectByAngle_AngleAboveFullCircle_NormalizesIntoOnMyWay()
        {
            Assert.AreEqual(PingType.OnMyWay, PingCommandModel.SelectByAngle(480f));
        }

        [Test]
        public void Constructor_NegativeMinInterval_ClampsToZero()
        {
            var model = new PingCommandModel(-1f, 4f);

            Assert.IsTrue(model.TryIssue(PingType.Danger, 0f, 0f, 1f));
            Assert.IsTrue(model.TryIssue(PingType.Attack, 1f, 1f, 1f));
            Assert.AreEqual(2, model.ActivePings.Count);
        }

        [Test]
        public void Constructor_NegativeDisplaySeconds_ClampsToImmediateExpiry()
        {
            var model = new PingCommandModel(0f, -1f);

            model.TryIssue(PingType.Danger, 0f, 0f, 3f);
            Assert.AreEqual(3f, model.ActivePings[0].ExpiresAt, Tolerance);

            model.Tick(3f);

            Assert.AreEqual(0, model.ActivePings.Count);
        }
    }
}
