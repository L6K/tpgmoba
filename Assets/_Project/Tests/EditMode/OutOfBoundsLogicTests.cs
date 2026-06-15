using NUnit.Framework;
using UnityEngine;
using Enigma.Character;

namespace Enigma.Tests
{
    /// <summary>
    /// OutOfBoundsLogic の純粋関数テスト。境界半径（レーン外周 51.8 / ポケット外周 17.4）と
    /// 救出地点（半径 45・同一角度）の整合を検証する。
    /// </summary>
    public sealed class OutOfBoundsLogicTests
    {
        private const float Epsilon = 0.01f;

        [Test]
        public void InsideLane_IsNotOutOfBounds()
        {
            // 中心距離 45（レーンアーク上）はプレイ領域
            Assert.IsFalse(OutOfBoundsLogic.IsOutOfBounds(45f, 0f));
        }

        [Test]
        public void OnWallTop_JustInside_IsNotOutOfBounds()
        {
            // 中心距離 51（壁体内 51.8 未満）はまだ場外でない
            Assert.IsFalse(OutOfBoundsLogic.IsOutOfBounds(51f, 0f));
        }

        [Test]
        public void InsidePocket_IsNotOutOfBounds()
        {
            // 赤ベース中心 (56,0) の直上はポケット内 → プレイ領域
            Assert.IsFalse(OutOfBoundsLogic.IsOutOfBounds(56f, 0f));
        }

        [Test]
        public void OutsidePocket_IsOutOfBounds()
        {
            // 赤ベース中心から +x へ 18（ポケット外周 17.4 超）かつ中心距離 74 → 場外
            Assert.IsTrue(OutOfBoundsLogic.IsOutOfBounds(74f, 0f));
        }

        [Test]
        public void FarOutside_IsOutOfBounds()
        {
            // 中心距離 60（51.8 超）でベースポケット外 → 場外
            Assert.IsTrue(OutOfBoundsLogic.IsOutOfBounds(0f, 60f));
        }

        [Test]
        public void JustOutsideLaneRing_IsOutOfBounds()
        {
            // 中心距離 52（51.8 超）でポケット外 → 場外
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
