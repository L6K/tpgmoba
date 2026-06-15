using NUnit.Framework;
using UnityEngine;
using Enigma.Minimap;

namespace Enigma.Tests
{
    /// <summary>
    /// MinimapMath.WorldToMap の純粋関数テスト。
    /// 依存なし・ランタイム不要なので EditMode で即時実行できる。
    /// </summary>
    public sealed class MinimapMathTests
    {
        // AetherRift_Map 設計値と一致させる
        private static readonly Rect   WorldBounds = new Rect(-100f, -70f, 200f, 140f);
        private static readonly Vector2 PanelSize  = new Vector2(220f, 154f);

        private const float Epsilon = 0.01f;

        [Test]
        public void Center_MapsTo_PanelCenter()
        {
            var result = MinimapMath.WorldToMap(Vector3.zero, WorldBounds, PanelSize);

            Assert.That(result.x, Is.EqualTo(110f).Within(Epsilon), "X: 中心は 110px");
            Assert.That(result.y, Is.EqualTo(77f).Within(Epsilon),  "Y: 中心は 77px");
        }

        [Test]
        public void NorthWest_Corner_MapsTo_TopLeft()
        {
            // xMin=-100, zMax=+70 → 左上(北西)角
            var result = MinimapMath.WorldToMap(new Vector3(-100f, 0f, 70f), WorldBounds, PanelSize);

            Assert.That(result.x, Is.EqualTo(0f).Within(Epsilon),   "X: 左端は 0px");
            Assert.That(result.y, Is.EqualTo(0f).Within(Epsilon),   "Y: 上端は 0px（北が上）");
        }

        [Test]
        public void SouthEast_Corner_MapsTo_BottomRight()
        {
            // xMax=+100, zMin=-70 → 右下(南東)角
            var result = MinimapMath.WorldToMap(new Vector3(100f, 0f, -70f), WorldBounds, PanelSize);

            Assert.That(result.x, Is.EqualTo(220f).Within(Epsilon), "X: 右端は 220px");
            Assert.That(result.y, Is.EqualTo(154f).Within(Epsilon), "Y: 下端は 154px");
        }

        [Test]
        public void OutOfBounds_X_IsClamped()
        {
            // x = +200 (範囲外) → 220px にクランプ
            var result = MinimapMath.WorldToMap(new Vector3(200f, 0f, 0f), WorldBounds, PanelSize);

            Assert.That(result.x, Is.EqualTo(220f).Within(Epsilon), "X: 範囲外は 220px にクランプ");
        }

        [Test]
        public void OutOfBounds_Z_IsClamped()
        {
            // z = -200 (範囲外) → 154px にクランプ
            var result = MinimapMath.WorldToMap(new Vector3(0f, 0f, -200f), WorldBounds, PanelSize);

            Assert.That(result.y, Is.EqualTo(154f).Within(Epsilon), "Y: 範囲外は 154px にクランプ");
        }

        [Test]
        public void North_IsUp_ZMax_YIsZero()
        {
            // z=+70(北端) → パネル y=0（上端）
            var result = MinimapMath.WorldToMap(new Vector3(0f, 0f, 70f), WorldBounds, PanelSize);

            Assert.That(result.y, Is.EqualTo(0f).Within(Epsilon), "北(z=+70)はパネル上端 y=0");
        }

        [Test]
        public void North_IsUp_ZMin_YIsPanelHeight()
        {
            // z=-70(南端) → パネル y=154（下端）
            var result = MinimapMath.WorldToMap(new Vector3(0f, 0f, -70f), WorldBounds, PanelSize);

            Assert.That(result.y, Is.EqualTo(154f).Within(Epsilon), "南(z=-70)はパネル下端 y=154");
        }
    }
}
