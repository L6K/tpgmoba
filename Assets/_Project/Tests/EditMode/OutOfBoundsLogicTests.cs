using NUnit.Framework;
using UnityEngine;
using Enigma.Character;

namespace Enigma.Tests
{
    /// <summary>
    /// OutOfBoundsLogic の純粋関数テスト。境界は目形(アーモンド)=上下2大円(中心(0,0,∓35)/r85)の
    /// AND 内側が場内。救出地点（半径 45・同一角度）の整合も検証する。
    /// </summary>
    public sealed class OutOfBoundsLogicTests
    {
        private const float Epsilon = 0.01f;

        [Test]
        public void InsideLane_IsNotOutOfBounds()
        {
            // (45, 0): 目形中央付近、両円の内側 → 場内
            Assert.IsFalse(OutOfBoundsLogic.IsOutOfBounds(45f, 0f));
        }

        [Test]
        public void NearCorner_JustInside_IsNotOutOfBounds()
        {
            // (75, 0): 目尻(±77.46, 0)の手前で両円の内側 → 場内
            Assert.IsFalse(OutOfBoundsLogic.IsOutOfBounds(75f, 0f));
        }

        [Test]
        public void RespawnPad_IsNotOutOfBounds()
        {
            // リスポーンパッド中心 (-68, 0) は目形内側 → 場内
            Assert.IsFalse(OutOfBoundsLogic.IsOutOfBounds(-68f, 0f));
        }

        [Test]
        public void PastCornerX_IsOutOfBounds()
        {
            // (80, 0): 目尻(±77.46, 0)を超えた → 場外
            Assert.IsTrue(OutOfBoundsLogic.IsOutOfBounds(80f, 0f));
        }

        [Test]
        public void PastTopLid_IsOutOfBounds()
        {
            // (0, 60): 中央縦幅 z=±50 を超えた → 場外
            Assert.IsTrue(OutOfBoundsLogic.IsOutOfBounds(0f, 60f));
        }

        [Test]
        public void JustPastTopLid_IsOutOfBounds()
        {
            // (0, 52): 中央縦幅 z=±50 を超えた → 場外
            Assert.IsTrue(OutOfBoundsLogic.IsOutOfBounds(0f, 52f));
        }

        [Test]
        public void NearestLanePoint_KeepsDirection()
        {
            // 場外点 (0, 60) → 同一角度（+z 方向）で半径 45 へ
            var (x, z) = OutOfBoundsLogic.NearestLanePoint(0f, 60f);
            Assert.That(x, Is.EqualTo(0f).Within(Epsilon),  "X は方向維持で 0");
            Assert.That(z, Is.EqualTo(45f).Within(Epsilon), "Z は半径 45 へ");
        }

        [Test]
        public void NearestLanePoint_DiagonalDirectionPreserved()
        {
            // 45度方向の場外点 → 同一角度・半径 45（成分は 45/√2）
            var (x, z) = OutOfBoundsLogic.NearestLanePoint(70f, 70f);
            float expected = 45f / Mathf.Sqrt(2f);
            Assert.That(x, Is.EqualTo(expected).Within(Epsilon), "X 成分");
            Assert.That(z, Is.EqualTo(expected).Within(Epsilon), "Z 成分");
            float resultDist = Mathf.Sqrt(x * x + z * z);
            Assert.That(resultDist, Is.EqualTo(45f).Within(Epsilon), "結果半径は 45");
        }

        [Test]
        public void NearestLanePoint_OriginFallsBackToPositiveX()
        {
            // 原点（角度未定義）は +x へ退避
            var (x, z) = OutOfBoundsLogic.NearestLanePoint(0f, 0f);
            Assert.That(x, Is.EqualTo(45f).Within(Epsilon));
            Assert.That(z, Is.EqualTo(0f).Within(Epsilon));
        }
    }
}
