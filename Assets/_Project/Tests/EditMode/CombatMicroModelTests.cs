using System.Collections.Generic;
using NUnit.Framework;
using Enigma.Character;

namespace Enigma.Tests
{
    public sealed class CombatMicroModelTests
    {
        private const float Tolerance = 0.01f;

        private static MicroContext Ctx(
            float myX = 0f, float myZ = 0f, float myHp = 1f,
            float range = 10f, bool ready = true, bool melee = false,
            float targetX = 5f, float targetZ = 0f, float targetHp = 1f,
            bool hasThreat = false, float threatX = 0f, float threatZ = 0f)
        {
            return new MicroContext(
                myX, myZ, myHp, range, ready, melee,
                targetX, targetZ, targetHp,
                hasThreat, threatX, threatZ);
        }

        [Test]
        public void OutOfRange_MovesTowardTarget()
        {
            var ctx = Ctx(myX: 0f, myZ: 0f, range: 10f, targetX: 20f, targetZ: 0f);
            var d = CombatMicroModel.Decide(in ctx);

            Assert.IsFalse(d.Attack);
            Assert.AreEqual(1f, d.MoveX, Tolerance);
            Assert.AreEqual(0f, d.MoveZ, Tolerance);
        }

        [Test]
        public void Ranged_InRange_Ready_Stops_AndAttacks()
        {
            // ideal = 10 * 0.85 = 8.5, kiteThreshold = 8.5*0.75 = 6.375。dist=8 は [6.375, 10] の範囲内
            var ctx = Ctx(myX: 0f, myZ: 0f, range: 10f, ready: true, melee: false, targetX: 8f, targetZ: 0f);
            var d = CombatMicroModel.Decide(in ctx);

            Assert.IsTrue(d.Attack);
            Assert.AreEqual(0f, d.MoveX, Tolerance);
            Assert.AreEqual(0f, d.MoveZ, Tolerance);
        }

        [Test]
        public void Ranged_TooClose_KitesAway()
        {
            // ideal = 8.5, kiteThreshold = 6.375。dist=3 < threshold → 離脱
            var ctx = Ctx(myX: 0f, myZ: 0f, range: 10f, ready: true, melee: false, targetX: 3f, targetZ: 0f);
            var d = CombatMicroModel.Decide(in ctx);

            Assert.AreEqual(-1f, d.MoveX, Tolerance);
            Assert.AreEqual(0f, d.MoveZ, Tolerance);
        }

        [Test]
        public void Ranged_OnCooldown_Strafes_PerpendicularToTarget()
        {
            var ctx = Ctx(myX: 0f, myZ: 0f, range: 10f, ready: false, melee: false, targetX: 8f, targetZ: 0f);
            var d = CombatMicroModel.Decide(in ctx);

            float dot = d.MoveX * 1f + d.MoveZ * 0f; // 対象方向 (1,0) との内積
            Assert.AreEqual(0f, dot, Tolerance);
            Assert.AreEqual(1f, Length(d.MoveX, d.MoveZ), Tolerance);
        }

        [Test]
        public void Melee_OutsideIdeal_MovesTowardTarget()
        {
            // melee ideal = range*0.6 = 3。dist=5 > ideal → 接近
            var ctx = Ctx(myX: 0f, myZ: 0f, range: 5f, ready: true, melee: true, targetX: 5f, targetZ: 0f);
            var d = CombatMicroModel.Decide(in ctx);

            Assert.AreEqual(1f, d.MoveX, Tolerance);
            Assert.AreEqual(0f, d.MoveZ, Tolerance);
        }

        [Test]
        public void Melee_WithinIdeal_Ready_Stops()
        {
            var ctx = Ctx(myX: 0f, myZ: 0f, range: 5f, ready: true, melee: true, targetX: 2f, targetZ: 0f);
            var d = CombatMicroModel.Decide(in ctx);

            Assert.IsTrue(d.Attack);
            Assert.AreEqual(0f, d.MoveX, Tolerance);
            Assert.AreEqual(0f, d.MoveZ, Tolerance);
        }

        [Test]
        public void Melee_WithinIdeal_OnCooldown_Strafes()
        {
            var ctx = Ctx(myX: 0f, myZ: 0f, range: 5f, ready: false, melee: true, targetX: 2f, targetZ: 0f);
            var d = CombatMicroModel.Decide(in ctx);

            float dot = d.MoveX * 1f + d.MoveZ * 0f;
            Assert.AreEqual(0f, dot, Tolerance);
            Assert.AreEqual(1f, Length(d.MoveX, d.MoveZ), Tolerance);
        }

        [Test]
        public void LowHp_WithNearThreat_MovesAwayFromThreat()
        {
            // 遠隔・停止条件（射程内・kiteThreshold以上・ready）だが低HP+至近脅威でオーバーレイ発動
            var ctx = Ctx(myX: 0f, myZ: 0f, myHp: 0.2f, range: 10f, ready: true, melee: false,
                          targetX: 8f, targetZ: 0f, hasThreat: true, threatX: 1f, threatZ: 0f);
            var d = CombatMicroModel.Decide(in ctx);

            // 脅威(1,0)から見て自分は原点。脅威から離れる方向は(-1,0)。
            // 素の移動(停止=0,0)とのブレンドなので、脅威逆方向への内積は負になるはず
            float dot = d.MoveX * (-1f) + d.MoveZ * 0f;
            Assert.Greater(dot, 0f); // (-1,0)方向への射影が正 = 脅威から離れる向き
            Assert.AreEqual(1f, Length(d.MoveX, d.MoveZ), Tolerance);
        }

        [Test]
        public void LowHp_ThreatFar_NoOverlay()
        {
            // 脅威はいるが射程*1.2より遠い → オーバーレイなし、通常の停止のまま
            var ctx = Ctx(myX: 0f, myZ: 0f, myHp: 0.2f, range: 10f, ready: true, melee: false,
                          targetX: 8f, targetZ: 0f, hasThreat: true, threatX: 50f, threatZ: 0f);
            var d = CombatMicroModel.Decide(in ctx);

            Assert.AreEqual(0f, d.MoveX, Tolerance);
            Assert.AreEqual(0f, d.MoveZ, Tolerance);
        }

        [Test]
        public void FocusTarget_PrefersChampionOverNonChampion()
        {
            var candidates = new List<FocusCandidate>
            {
                new FocusCandidate(0f, 0f, 0.9f, isChampion: false),
                new FocusCandidate(0f, 0f, 0.9f, isChampion: true),
            };

            int idx = CombatMicroModel.ChooseFocusTarget(candidates, -1, 0f, 0f);
            Assert.AreEqual(1, idx);
        }

        [Test]
        public void FocusTarget_PicksLowestHpWithinSameClass()
        {
            var candidates = new List<FocusCandidate>
            {
                new FocusCandidate(0f, 0f, 0.8f, isChampion: true),
                new FocusCandidate(0f, 0f, 0.3f, isChampion: true),
                new FocusCandidate(0f, 0f, 0.5f, isChampion: true),
            };

            int idx = CombatMicroModel.ChooseFocusTarget(candidates, -1, 0f, 0f);
            Assert.AreEqual(1, idx);
        }

        [Test]
        public void FocusTarget_Hysteresis_KeepsCurrent_WhenDifferenceBelowMargin()
        {
            var candidates = new List<FocusCandidate>
            {
                new FocusCandidate(0f, 0f, 0.5f, isChampion: true), // current
                new FocusCandidate(0f, 0f, 0.4f, isChampion: true), // diff 0.1 < 0.15
            };

            int idx = CombatMicroModel.ChooseFocusTarget(candidates, 0, 0f, 0f);
            Assert.AreEqual(0, idx);
        }

        [Test]
        public void FocusTarget_SwitchesWhenDifferenceExceedsMargin()
        {
            var candidates = new List<FocusCandidate>
            {
                new FocusCandidate(0f, 0f, 0.5f, isChampion: true), // current
                new FocusCandidate(0f, 0f, 0.3f, isChampion: true), // diff 0.2 > 0.15
            };

            int idx = CombatMicroModel.ChooseFocusTarget(candidates, 0, 0f, 0f);
            Assert.AreEqual(1, idx);
        }

        [Test]
        public void FocusTarget_ClassUpgrade_IgnoresHysteresis()
        {
            var candidates = new List<FocusCandidate>
            {
                new FocusCandidate(0f, 0f, 0.9f, isChampion: false), // current, high hp
                new FocusCandidate(0f, 0f, 0.95f, isChampion: true), // champion, even higher hp
            };

            int idx = CombatMicroModel.ChooseFocusTarget(candidates, 0, 0f, 0f);
            Assert.AreEqual(1, idx);
        }

        [Test]
        public void FocusTarget_EmptyList_ReturnsMinusOne()
        {
            int idx = CombatMicroModel.ChooseFocusTarget(new List<FocusCandidate>(), -1, 0f, 0f);
            Assert.AreEqual(-1, idx);
        }

        private static float Length(float x, float z)
        {
            return (float)System.Math.Sqrt(x * x + z * z);
        }
    }
}
