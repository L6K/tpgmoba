using Enigma.Map;
using NUnit.Framework;

namespace Enigma.Tests
{
    public sealed class GimmickPhysicsModelTests
    {
        private const float Tolerance = 0.001f;

        [Test]
        public void LaunchToTarget_FlatTarget_UsesArcPeakAndLandsAtTarget()
        {
            var velocity = GimmickPhysicsModel.LaunchToTarget(
                0f, 0f, 0f,
                10f, 0f, 0f,
                20f,
                5f);

            float expectedVy = (float)System.Math.Sqrt(2f * 20f * 5f);
            float expectedTravel = expectedVy / 20f + (float)System.Math.Sqrt(2f * 5f / 20f);

            Assert.AreEqual(expectedVy, velocity.Vy, Tolerance);
            Assert.AreEqual(10f / expectedTravel, velocity.Vx, Tolerance);
            Assert.AreEqual(0f, velocity.Vz, Tolerance);
            Assert.AreEqual(expectedTravel, velocity.TravelSeconds, Tolerance);
            AssertLandsAtTarget(0f, 0f, 0f, velocity, 20f, 10f, 0f, 0f);
        }

        [Test]
        public void LaunchToTarget_HigherTarget_UsesTargetPlusArcPeakAndLandsAtTarget()
        {
            var velocity = GimmickPhysicsModel.LaunchToTarget(
                -2f, 1f, 3f,
                4f, 6f, -5f,
                12f,
                4f);

            float peakY = 10f;
            float expectedVy = (float)System.Math.Sqrt(2f * 12f * (peakY - 1f));
            float expectedTravel = expectedVy / 12f + (float)System.Math.Sqrt(2f * (peakY - 6f) / 12f);

            Assert.AreEqual(expectedVy, velocity.Vy, Tolerance);
            Assert.AreEqual((4f - -2f) / expectedTravel, velocity.Vx, Tolerance);
            Assert.AreEqual((-5f - 3f) / expectedTravel, velocity.Vz, Tolerance);
            AssertLandsAtTarget(-2f, 1f, 3f, velocity, 12f, 4f, 6f, -5f);
        }

        [Test]
        public void LaunchToTarget_InvalidGravityAndArcHeight_FallsBackToDefaults()
        {
            var invalidGravity = GimmickPhysicsModel.LaunchToTarget(
                0f, 0f, 0f,
                0f, 0f, 2f,
                0f,
                2f);
            var defaultGravity = GimmickPhysicsModel.LaunchToTarget(
                0f, 0f, 0f,
                0f, 0f, 2f,
                9.8f,
                2f);

            Assert.AreEqual(defaultGravity.Vy, invalidGravity.Vy, Tolerance);
            Assert.AreEqual(defaultGravity.TravelSeconds, invalidGravity.TravelSeconds, Tolerance);
            Assert.AreEqual(defaultGravity.Vz, invalidGravity.Vz, Tolerance);

            var invalidArc = GimmickPhysicsModel.LaunchToTarget(
                0f, 0f, 0f,
                0f, 0f, 2f,
                9.8f,
                0f);
            var defaultArc = GimmickPhysicsModel.LaunchToTarget(
                0f, 0f, 0f,
                0f, 0f, 2f,
                9.8f,
                1f);

            Assert.AreEqual(defaultArc.Vy, invalidArc.Vy, Tolerance);
            Assert.AreEqual(defaultArc.TravelSeconds, invalidArc.TravelSeconds, Tolerance);
            Assert.AreEqual(defaultArc.Vz, invalidArc.Vz, Tolerance);
        }

        [Test]
        public void GravityWellAccel_OutsideRadius_ReturnsZero()
        {
            GimmickPhysicsModel.GravityWellAccel(
                6f, 0f,
                0f, 0f,
                5f,
                10f,
                out float ax,
                out float az);

            Assert.AreEqual(0f, ax, Tolerance);
            Assert.AreEqual(0f, az, Tolerance);
        }

        [Test]
        public void GravityWellAccel_InsideRadius_AcceleratesTowardCenterWithLinearFalloff()
        {
            GimmickPhysicsModel.GravityWellAccel(
                0f, 0f,
                10f, 0f,
                20f,
                8f,
                out float ax,
                out float az);

            Assert.AreEqual(4f, ax, Tolerance);
            Assert.AreEqual(0f, az, Tolerance);
        }

        [Test]
        public void GravityWellAccel_NearCenter_ReturnsZero()
        {
            GimmickPhysicsModel.GravityWellAccel(
                1f, 1f,
                1.00001f, 1f,
                10f,
                5f,
                out float ax,
                out float az);

            Assert.AreEqual(0f, ax, Tolerance);
            Assert.AreEqual(0f, az, Tolerance);
        }

        [Test]
        public void GateSlowMultiplier_InsideAppliesClampedSlowStrength()
        {
            Assert.AreEqual(0.7f, GimmickPhysicsModel.GateSlowMultiplier(true, 0.3f), Tolerance);
            Assert.AreEqual(1f, GimmickPhysicsModel.GateSlowMultiplier(true, -2f), Tolerance);
            Assert.AreEqual(0f, GimmickPhysicsModel.GateSlowMultiplier(true, 2f), Tolerance);
        }

        [Test]
        public void GateSlowMultiplier_OutsideReturnsOne()
        {
            Assert.AreEqual(1f, GimmickPhysicsModel.GateSlowMultiplier(false, 0.75f), Tolerance);
            Assert.AreEqual(1f, GimmickPhysicsModel.GateSlowMultiplier(false, 2f), Tolerance);
        }

        private static void AssertLandsAtTarget(
            float fromX,
            float fromY,
            float fromZ,
            LaunchVelocity velocity,
            float gravity,
            float expectedX,
            float expectedY,
            float expectedZ)
        {
            float t = velocity.TravelSeconds;
            float x = fromX + velocity.Vx * t;
            float y = fromY + velocity.Vy * t - 0.5f * gravity * t * t;
            float z = fromZ + velocity.Vz * t;

            Assert.AreEqual(expectedX, x, Tolerance);
            Assert.AreEqual(expectedY, y, Tolerance);
            Assert.AreEqual(expectedZ, z, Tolerance);
        }
    }
}
