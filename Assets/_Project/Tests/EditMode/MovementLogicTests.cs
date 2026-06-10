using NUnit.Framework;
using UnityEngine;
using Enigma.Character;

namespace Enigma.Tests
{
    public sealed class MovementLogicTests
    {
        private const float Tolerance = 0.001f;

        [Test]
        public void CameraRelativeMove_Yaw0_Input0_1_Returns_PlusZ()
        {
            var result = MovementLogic.CameraRelativeMove(new Vector2(0f, 1f), 0f);
            Assert.AreEqual(0f, result.x, Tolerance);
            Assert.AreEqual(0f, result.y, Tolerance);
            Assert.AreEqual(1f, result.z, Tolerance);
        }

        [Test]
        public void CameraRelativeMove_Yaw90_Input0_1_Returns_PlusX()
        {
            // カメラが90度回転している場合、前方入力 (0,1) はワールド +X 方向
            var result = MovementLogic.CameraRelativeMove(new Vector2(0f, 1f), 90f);
            Assert.AreEqual(1f, result.x, Tolerance);
            Assert.AreEqual(0f, result.y, Tolerance);
            Assert.AreEqual(0f, result.z, Tolerance);
        }

        [Test]
        public void CameraRelativeMove_ZeroInput_ReturnsZero()
        {
            var result = MovementLogic.CameraRelativeMove(Vector2.zero, 45f);
            Assert.AreEqual(Vector3.zero, result);
        }

        [Test]
        public void CameraRelativeMove_NonZeroInput_IsNormalized()
        {
            var result = MovementLogic.CameraRelativeMove(new Vector2(1f, 1f), 30f);
            Assert.AreEqual(1f, result.magnitude, Tolerance);
        }
    }
}
