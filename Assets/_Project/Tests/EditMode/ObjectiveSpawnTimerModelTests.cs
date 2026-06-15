using Enigma.GameModes;
using NUnit.Framework;

namespace Enigma.Tests
{
    public sealed class ObjectiveSpawnTimerModelTests
    {
        private const float Tolerance = 1e-4f;

        [Test]
        public void GetState_AtStartBeforeWarningWindow_ReturnsDormant()
        {
            var model = new ObjectiveSpawnTimerModel(120f, 90f, 30f);

            Assert.AreEqual(ObjectiveState.Dormant, model.GetState(0f));
            Assert.IsFalse(model.IsActive(0f));
        }

        [Test]
        public void SecondsUntilSpawn_BeforeWarningWindow_DecreasesFromNextSpawn()
        {
            var model = new ObjectiveSpawnTimerModel(120f, 90f, 30f);

            Assert.AreEqual(ObjectiveState.Dormant, model.GetState(50f));
            Assert.AreEqual(70f, model.SecondsUntilSpawn(50f), Tolerance);
        }

        [Test]
        public void GetState_InsideWarningWindow_ReturnsWarning()
        {
            var model = new ObjectiveSpawnTimerModel(120f, 90f, 30f);

            Assert.AreEqual(ObjectiveState.Warning, model.GetState(100f));
            Assert.IsTrue(model.IsWarning(100f));
        }

        [Test]
        public void GetState_AtWarningBoundary_ReturnsWarning()
        {
            var model = new ObjectiveSpawnTimerModel(120f, 90f, 30f);

            Assert.AreEqual(ObjectiveState.Warning, model.GetState(90f));
        }

        [Test]
        public void GetState_AtFirstSpawnBoundary_ReturnsActive()
        {
            var model = new ObjectiveSpawnTimerModel(120f, 90f, 30f);

            Assert.AreEqual(ObjectiveState.Active, model.GetState(120f));
            Assert.IsTrue(model.IsActive(120f));
        }

        [Test]
        public void GetState_AfterFirstSpawn_ReturnsActive()
        {
            var model = new ObjectiveSpawnTimerModel(120f, 90f, 30f);

            Assert.AreEqual(ObjectiveState.Active, model.GetState(121f));
        }

        [Test]
        public void SecondsUntilSpawn_WhileActive_ReturnsZero()
        {
            var model = new ObjectiveSpawnTimerModel(120f, 90f, 30f);

            Assert.AreEqual(0f, model.SecondsUntilSpawn(150f), Tolerance);
        }

        [Test]
        public void NotifyKilled_WhileActive_SchedulesRespawn()
        {
            var model = new ObjectiveSpawnTimerModel(120f, 90f, 30f);

            model.NotifyKilled(130f);

            Assert.AreEqual(ObjectiveState.Dormant, model.GetState(131f));
            Assert.AreEqual(89f, model.SecondsUntilSpawn(131f), Tolerance);
        }

        [Test]
        public void NotifyKilled_RespawnUsesNewWarningAndActiveBoundaries()
        {
            var model = new ObjectiveSpawnTimerModel(120f, 90f, 30f);

            model.NotifyKilled(130f);

            Assert.AreEqual(ObjectiveState.Dormant, model.GetState(189.9f));
            Assert.AreEqual(ObjectiveState.Warning, model.GetState(190f));
            Assert.AreEqual(ObjectiveState.Active, model.GetState(220f));
        }

        [Test]
        public void NotifyKilled_WhenNotActive_IsIgnored()
        {
            var model = new ObjectiveSpawnTimerModel(120f, 90f, 30f);

            model.NotifyKilled(10f);

            Assert.AreEqual(110f, model.SecondsUntilSpawn(10f), Tolerance);
            Assert.AreEqual(ObjectiveState.Warning, model.GetState(90f));
        }

        [Test]
        public void NotifyKilled_MultipleCycles_UpdateNextSpawnCorrectly()
        {
            var model = new ObjectiveSpawnTimerModel(10f, 5f, 2f);

            model.NotifyKilled(10f);
            Assert.AreEqual(ObjectiveState.Active, model.GetState(15f));

            model.NotifyKilled(15f);

            Assert.AreEqual(ObjectiveState.Dormant, model.GetState(17.9f));
            Assert.AreEqual(ObjectiveState.Warning, model.GetState(18f));
            Assert.AreEqual(ObjectiveState.Active, model.GetState(20f));
        }

        [Test]
        public void SecondsUntilSpawn_NeverReturnsNegative()
        {
            var model = new ObjectiveSpawnTimerModel(10f, 5f, 2f);

            Assert.AreEqual(0f, model.SecondsUntilSpawn(100f), Tolerance);
        }

        [Test]
        public void Reset_RestoresInitialNextSpawn()
        {
            var model = new ObjectiveSpawnTimerModel(120f, 90f, 30f);

            model.NotifyKilled(130f);
            model.Reset();

            Assert.AreEqual(ObjectiveState.Dormant, model.GetState(0f));
            Assert.AreEqual(120f, model.SecondsUntilSpawn(0f), Tolerance);
            Assert.AreEqual(ObjectiveState.Active, model.GetState(120f));
        }

        [Test]
        public void Constructor_NegativeFirstSpawnDelay_ClampsToImmediateActive()
        {
            var model = new ObjectiveSpawnTimerModel(-5f, 90f, 30f);

            Assert.AreEqual(ObjectiveState.Active, model.GetState(0f));
            Assert.AreEqual(0f, model.SecondsUntilSpawn(0f), Tolerance);
        }

        [Test]
        public void Constructor_NegativeWarningLead_ClampsToNoWarningWindow()
        {
            var model = new ObjectiveSpawnTimerModel(10f, 5f, -1f);

            Assert.AreEqual(ObjectiveState.Dormant, model.GetState(9.9f));
            Assert.AreEqual(ObjectiveState.Active, model.GetState(10f));
        }
    }
}
