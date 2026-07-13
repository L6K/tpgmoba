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
    }
}
