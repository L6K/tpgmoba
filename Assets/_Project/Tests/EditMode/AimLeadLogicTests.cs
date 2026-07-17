using NUnit.Framework;
using UnityEngine;
using Enigma.Character;

namespace Enigma.Tests
{
    public sealed class AimLeadLogicTests
    {
        // 静止対象: 速度ゼロなら現在位置がそのまま狙い点になる
        [Test]
        public void Stationary_Target_Returns_Current_Position()
        {
            var result = AimLeadLogic.PredictAimPoint(
                Vector3.zero, new Vector3(10f, 0f, 0f), Vector3.zero, 20f);
            Assert.AreEqual(new Vector3(10f, 0f, 0f), result);
        }

        // 等速移動対象: t = dist/speed = 10/20 = 0.5s 分だけ進めた点を狙う
        [Test]
        public void Moving_Target_Returns_Led_Position()
        {
            var result = AimLeadLogic.PredictAimPoint(
                Vector3.zero, new Vector3(10f, 0f, 0f), new Vector3(0f, 0f, 5f), 20f);
            Assert.AreEqual(new Vector3(10f, 0f, 2.5f), result);
        }

        // 弾速0（即着弾扱い）は速度に関わらず現在位置を返す
        [Test]
        public void Zero_Projectile_Speed_Returns_Current_Position()
        {
            var result = AimLeadLogic.PredictAimPoint(
                Vector3.zero, new Vector3(10f, 0f, 0f), new Vector3(0f, 0f, 5f), 0f);
            Assert.AreEqual(new Vector3(10f, 0f, 0f), result);
        }

        // 到達時間は1.5秒でクランプされる（過剰リード防止）
        // dist=100, speed=10 → t=10s だが 1.5s にクランプ → 100 + vel*1.5
        [Test]
        public void Lead_Time_Is_Clamped_To_Max()
        {
            var result = AimLeadLogic.PredictAimPoint(
                Vector3.zero, new Vector3(100f, 0f, 0f), new Vector3(0f, 0f, 4f), 10f);
            Assert.AreEqual(new Vector3(100f, 0f, 6f), result);
        }

        // 静止対象（速度ゼロ）: 現在位置がそのまま予測地点になる
        [Test]
        public void PredictGroundPoint_Stationary_Target_Returns_Current_Position()
        {
            var result = AimLeadLogic.PredictGroundPoint(
                new Vector3(10f, 0f, 0f), Vector3.zero, 0.8f);
            Assert.AreEqual(new Vector3(10f, 0f, 0f), result);
        }

        // 等速移動対象: target(10,0,0) + vel(0,0,4)*delay(0.5) = (10,0,2)
        [Test]
        public void PredictGroundPoint_Moving_Target_Returns_Led_Position()
        {
            var result = AimLeadLogic.PredictGroundPoint(
                new Vector3(10f, 0f, 0f), new Vector3(0f, 0f, 4f), 0.5f);
            Assert.AreEqual(new Vector3(10f, 0f, 2f), result);
        }

        // delay は 1.2s で頭打ち（過剰リード防止）
        // vel(0,0,4)*1.2 = (0,0,4.8) → target(10,0,0)+それ = (10,0,4.8)
        [Test]
        public void PredictGroundPoint_Delay_Is_Clamped_To_Max()
        {
            var result = AimLeadLogic.PredictGroundPoint(
                new Vector3(10f, 0f, 0f), new Vector3(0f, 0f, 4f), 2.0f);
            Assert.AreEqual(new Vector3(10f, 0f, 4.8f), result);
        }

        // 負の delay は 0 扱い（安全側: 現在位置のまま）
        [Test]
        public void PredictGroundPoint_Negative_Delay_Is_Treated_As_Zero()
        {
            var result = AimLeadLogic.PredictGroundPoint(
                new Vector3(10f, 0f, 0f), new Vector3(0f, 0f, 4f), -1f);
            Assert.AreEqual(new Vector3(10f, 0f, 0f), result);
        }

        // 射程内: そのまま point を返す
        [Test]
        public void ClampToRange_Within_Range_Returns_Point_Unchanged()
        {
            var result = AimLeadLogic.ClampToRange(
                Vector3.zero, new Vector3(5f, 0f, 0f), 10f);
            Assert.AreEqual(new Vector3(5f, 0f, 0f), result);
        }

        // 射程超過: shooter→point 方向を保ったまま距離を range にクランプ
        // shooter=(0,0,0), point=(20,0,0), range=10 → (10,0,0)
        [Test]
        public void ClampToRange_Beyond_Range_Clamps_Along_Direction()
        {
            var result = AimLeadLogic.ClampToRange(
                Vector3.zero, new Vector3(20f, 0f, 0f), 10f);
            Assert.AreEqual(new Vector3(10f, 0f, 0f), result);
        }

        // shooter が原点でない場合も方向を保ってクランプされる
        // shooter=(5,0,0), point=(5,0,30), range=10 → (5,0,10)
        [Test]
        public void ClampToRange_Beyond_Range_From_NonOrigin_Shooter()
        {
            var result = AimLeadLogic.ClampToRange(
                new Vector3(5f, 0f, 0f), new Vector3(5f, 0f, 30f), 10f);
            Assert.AreEqual(new Vector3(5f, 0f, 10f), result);
        }
    }
}
