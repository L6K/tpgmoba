using NUnit.Framework;
using UnityEngine;
using Enigma.Combat;

namespace Enigma.Tests.EditMode
{
    public sealed class TelegraphSectorTests
    {
        private static readonly Vector3 Origin    = Vector3.zero;
        private static readonly Vector3 Direction = Vector3.forward; // (0,0,1)
        private const float Angle  = 90f;
        private const float Radius = 10f;

        [Test]
        public void IsInsideSector_PointDirectlyAhead_ReturnsTrue()
        {
            var point = new Vector3(0f, 0f, 5f); // 正面内側
            Assert.IsTrue(TelegraphSector.IsInsideSector(Origin, Direction, Angle, Radius, point));
        }

        [Test]
        public void IsInsideSector_PointOutsideAngle_ReturnsFalse()
        {
            // 真後ろは 180°で Angle/2=45° を大幅に超える
            var point = new Vector3(0f, 0f, -5f);
            Assert.IsFalse(TelegraphSector.IsInsideSector(Origin, Direction, Angle, Radius, point));
        }

        [Test]
        public void IsInsideSector_PointOutsideRadius_ReturnsFalse()
        {
            var point = new Vector3(0f, 0f, 15f); // 正面だが半径外
            Assert.IsFalse(TelegraphSector.IsInsideSector(Origin, Direction, Angle, Radius, point));
        }

        [Test]
        public void IsInsideSector_PointOnRadiusBoundary_ReturnsTrue()
        {
            // ちょうど radius の距離・正面
            var point = new Vector3(0f, 0f, Radius);
            Assert.IsTrue(TelegraphSector.IsInsideSector(Origin, Direction, Angle, Radius, point));
        }

        [Test]
        public void IsInsideSector_PointOnAngleBoundary_ReturnsTrue()
        {
            // ちょうど Angle/2 = 45° の辺（45° は XZ で x==z の方向）
            float d = Radius * 0.5f;
            var point = new Vector3(d, 0f, d); // 45° 斜め前
            Assert.IsTrue(TelegraphSector.IsInsideSector(Origin, Direction, Angle, Radius, point));
        }
    }
}
