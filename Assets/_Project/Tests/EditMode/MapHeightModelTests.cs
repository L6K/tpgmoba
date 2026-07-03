using NUnit.Framework;
using UnityEngine;
using Enigma.Map;

namespace Enigma.Tests
{
    /// <summary>
    /// MapHeightModel の高さ関数テスト。クレーター(r&lt;22)/川(|x|&lt;=9, 22&lt;=r&lt;=54)/
    /// 基地プラトー(|x|&gt;=86)の3領域が期待どおりの高さ・優先順位で計算されることを検証する。
    /// </summary>
    public sealed class MapHeightModelTests
    {
        private const float Epsilon = 0.01f;

        [Test]
        public void CraterCenter_IsFloorDepth()
        {
            Assert.That(MapHeightModel.Height(0f, 0f), Is.EqualTo(-2.5f).Within(Epsilon));
        }

        [Test]
        public void CraterFloorEdge_IsStillFloorDepth()
        {
            // r=10 はクレーター床(r<=14)の内側 → 底のまま
            Assert.That(MapHeightModel.Height(10f, 0f), Is.EqualTo(-2.5f).Within(Epsilon));
        }

        [Test]
        public void CraterSlope_IsBetweenFloorAndZero()
        {
            // r=18 はクレーター床(14)と縁(22)の中間 → 補間区間
            float h = MapHeightModel.Height(18f, 0f);
            Assert.That(h, Is.GreaterThan(-2.5f));
            Assert.That(h, Is.LessThan(0f));
        }

        [Test]
        public void RiverCenterLine_IsFullDepth()
        {
            // (0,40): 川の中心線・半径帯22-54の中央 → falloff全て1 → 満水深
            Assert.That(MapHeightModel.Height(0f, 40f), Is.EqualTo(-1.2f).Within(Epsilon));
        }

        [Test]
        public void RiverNearBank_FallsOffTowardZero()
        {
            // (8.99,40): 川岸ぎりぎり(|x|<=9)。中心からのfalloffがほぼ0 → 0近傍
            float h = MapHeightModel.Height(8.99f, 40f);
            Assert.That(h, Is.EqualTo(0f).Within(0.02f));
        }

        [Test]
        public void OutsideRiverBand_IsFlat()
        {
            // (0,56): 川の半径帯(22-54)を超えた外側 → 平地
            Assert.That(MapHeightModel.Height(0f, 56f), Is.EqualTo(0f).Within(Epsilon));
        }

        [Test]
        public void LaneRing_IsFlat()
        {
            // (63,0): レーン円環上、いずれの特殊領域にも属さない → 平地
            Assert.That(MapHeightModel.Height(63f, 0f), Is.EqualTo(0f).Within(Epsilon));
        }

        [Test]
        public void PlateauRampMidpoint_IsHalfHeight()
        {
            // (89,0): ランプ区間(86-92)の中間・ゲート幅内(|z|<=10) → プラトー高の半分
            Assert.That(MapHeightModel.Height(89f, 0f), Is.EqualTo(1.25f).Within(Epsilon));
        }

        [Test]
        public void PlateauRampOutsideGateWidth_IsCliffFlat()
        {
            // (89,20): ランプ区間だが |z|>10 でゲート幅外 → 崖扱いで平地(登攀不可)
            Assert.That(MapHeightModel.Height(89f, 20f), Is.EqualTo(0f).Within(Epsilon));
        }

        [Test]
        public void PlateauInterior_IsFullHeight()
        {
            Assert.That(MapHeightModel.Height(100f, 0f), Is.EqualTo(2.5f).Within(Epsilon));
        }

        [Test]
        public void PlateauInterior_NegativeX_IsFullHeight()
        {
            Assert.That(MapHeightModel.Height(-100f, 0f), Is.EqualTo(2.5f).Within(Epsilon));
        }

        [Test]
        public void FarField_OutsideAllRegions_IsFlat()
        {
            Assert.That(MapHeightModel.Height(0f, 70f), Is.EqualTo(0f).Within(Epsilon));
        }

        [Test]
        public void Diagonal_OutsideAllRegions_IsFlat()
        {
            // (35,35): r≈49.5 だが |x|=35>9 で川域外、|x|<86 でプラトー域外 → 平地
            Assert.That(MapHeightModel.Height(35f, 35f), Is.EqualTo(0f).Within(Epsilon));
        }

        [Test]
        public void NearBoundary_IsStillPlateau()
        {
            // (108,0): |x|=108>=92(PlateauOuterX) → プラトー内部(境界付近も含め+2.5で一定)
            Assert.That(MapHeightModel.Height(108f, 0f), Is.EqualTo(2.5f).Within(Epsilon));
        }
    }
}
