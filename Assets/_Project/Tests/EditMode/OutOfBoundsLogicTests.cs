using NUnit.Framework;
using UnityEngine;
using Enigma.Character;

namespace Enigma.Tests
{
    /// <summary>
    /// OutOfBoundsLogic の純粋関数テスト。境界は目形(アーモンド)=上下2大円(中心(0,0,∓48)/r120)の
    /// AND 内側が場内。救出地点（半径 63・同一角度）の整合も検証する。
    /// M-0(平面1.4倍拡張): 旧 R=85/B=35 → 新 R=120/B=48。目尻 x = sqrt(120^2-48^2) ≈ 109.98。
    /// </summary>
    public sealed class OutOfBoundsLogicTests
    {
        private const float Epsilon = 0.01f;

        [Test]
        public void InsideLane_IsNotOutOfBounds()
        {
            // (63, 0): 目形中央付近、両円の内側 → 場内
            Assert.IsFalse(OutOfBoundsLogic.IsOutOfBounds(63f, 0f));
        }

        [Test]
        public void NearCorner_JustInside_IsNotOutOfBounds()
        {
            // (105, 0): 目尻(±109.98, 0)の手前で両円の内側 → 場内
            Assert.IsFalse(OutOfBoundsLogic.IsOutOfBounds(105f, 0f));
        }

        [Test]
        public void RespawnPad_IsNotOutOfBounds()
        {
            // リスポーンパッド中心 (-100, 0) は目形内側 → 場内
            Assert.IsFalse(OutOfBoundsLogic.IsOutOfBounds(-100f, 0f));
        }

        [Test]
        public void PastCornerX_IsOutOfBounds()
        {
            // (113, 0): 目尻(±109.98, 0)を超えた → 場外
            Assert.IsTrue(OutOfBoundsLogic.IsOutOfBounds(113f, 0f));
        }

        [Test]
        public void PastTopLid_IsOutOfBounds()
        {
            // (0, 84): 中央縦幅 z=±72 を超えた → 場外
            Assert.IsTrue(OutOfBoundsLogic.IsOutOfBounds(0f, 84f));
        }

        [Test]
        public void JustPastTopLid_IsOutOfBounds()
        {
            // (0, 74): 中央縦幅 z=±72 を超えた → 場外
            Assert.IsTrue(OutOfBoundsLogic.IsOutOfBounds(0f, 74f));
        }

        [Test]
        public void NearestLanePoint_KeepsDirection()
        {
            // 場外点 (0, 84) → 同一角度（+z 方向）で半径 63 へ
            var (x, z) = OutOfBoundsLogic.NearestLanePoint(0f, 84f);
            Assert.That(x, Is.EqualTo(0f).Within(Epsilon),  "X は方向維持で 0");
            Assert.That(z, Is.EqualTo(63f).Within(Epsilon), "Z は半径 63 へ");
        }

        [Test]
        public void NearestLanePoint_DiagonalDirectionPreserved()
        {
            // 45度方向の場外点 → 同一角度・半径 63（成分は 63/√2）
            var (x, z) = OutOfBoundsLogic.NearestLanePoint(98f, 98f);
            float expected = 63f / Mathf.Sqrt(2f);
            Assert.That(x, Is.EqualTo(expected).Within(Epsilon), "X 成分");
            Assert.That(z, Is.EqualTo(expected).Within(Epsilon), "Z 成分");
            float resultDist = Mathf.Sqrt(x * x + z * z);
            Assert.That(resultDist, Is.EqualTo(63f).Within(Epsilon), "結果半径は 63");
        }

        [Test]
        public void NearestLanePoint_OriginFallsBackToPositiveX()
        {
            // 原点（角度未定義）は +x へ退避
            var (x, z) = OutOfBoundsLogic.NearestLanePoint(0f, 0f);
            Assert.That(x, Is.EqualTo(63f).Within(Epsilon));
            Assert.That(z, Is.EqualTo(0f).Within(Epsilon));
        }
    }
}
