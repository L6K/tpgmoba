using NUnit.Framework;
using Enigma.Character;

namespace Enigma.Tests
{
    public sealed class PatrolFreezeLogicTests
    {
        [Test]
        public void NotFrozen_WhenMovedEnough()
        {
            // 閾値以上動けていれば、経過時間に関わらず凍結ではない。
            Assert.IsFalse(PatrolFreezeLogic.IsFrozen(
                movedSinceAnchor: PatrolFreezeLogic.FreezeMoveEpsilon + 0.1f,
                elapsedSinceAnchor: PatrolFreezeLogic.FreezeTimeout + 5f));
        }

        [Test]
        public void NotFrozen_WhenStillWithinTimeout()
        {
            // ほぼ動けていなくても、まだ猶予時間内なら凍結発動しない(瞬間的な停止で誤爆させない)。
            Assert.IsFalse(PatrolFreezeLogic.IsFrozen(
                movedSinceAnchor: 0f,
                elapsedSinceAnchor: PatrolFreezeLogic.FreezeTimeout - 0.5f));
        }

        [Test]
        public void Frozen_WhenNoProgressPastTimeout()
        {
            // 閾値未満の移動しかない状態が猶予時間を超えたら凍結発動。
            Assert.IsTrue(PatrolFreezeLogic.IsFrozen(
                movedSinceAnchor: PatrolFreezeLogic.FreezeMoveEpsilon - 0.01f,
                elapsedSinceAnchor: PatrolFreezeLogic.FreezeTimeout));
        }

        [Test]
        public void Frozen_ExactlyAtTimeout_WithZeroMovement()
        {
            // ちょうど FreezeTimeout・移動ゼロは境界として発動側に含める。
            Assert.IsTrue(PatrolFreezeLogic.IsFrozen(
                movedSinceAnchor: 0f,
                elapsedSinceAnchor: PatrolFreezeLogic.FreezeTimeout));
        }

        // ── 適用範囲: ジャングラー/レーナー ──

        [Test]
        public void WatchdogApplies_Jungler_Always()
        {
            // ジャングラーはどこでも経路詰まりし得るため常に有効(レーン相当の遠方でも)。
            Assert.IsTrue(PatrolFreezeLogic.WatchdogApplies(isJungler: true, distFromCenter: 5f));
            Assert.IsTrue(PatrolFreezeLogic.WatchdogApplies(isJungler: true, distFromCenter: 63f));
        }

        [Test]
        public void WatchdogApplies_Laner_InsideJungle_True()
        {
            // レーナーがジャングル内(r<54)へ引き込まれて詰まったときは有効。
            Assert.IsTrue(PatrolFreezeLogic.WatchdogApplies(isJungler: false, distFromCenter: 40f));
        }

        [Test]
        public void WatchdogApplies_Laner_OnLane_False()
        {
            // レーナーがレーン上・敵ベース前(r>=54)に居る停滞は正当なので介入しない。
            Assert.IsFalse(PatrolFreezeLogic.WatchdogApplies(isJungler: false, distFromCenter: 63f));
        }

        [Test]
        public void WatchdogApplies_Laner_AtBoundary_False()
        {
            // 境界 r=54 ちょうどは「ジャングル外」側(除外)。壁帯 r54〜55.5 の内縁を境界とする。
            Assert.IsFalse(PatrolFreezeLogic.WatchdogApplies(
                isJungler: false, distFromCenter: PatrolFreezeLogic.LanerJungleRadius));
        }

        // ── マクロ・ロール込みの適用可否(WatchdogEligible) ──

        [Test]
        public void Eligible_Jungler_FarmOrPush_True()
        {
            Assert.IsTrue(PatrolFreezeLogic.WatchdogEligible(true, BotMacroAction.Farm, 20f));
            Assert.IsTrue(PatrolFreezeLogic.WatchdogEligible(true, BotMacroAction.Push, 20f));
        }

        [Test]
        public void Eligible_Jungler_Retreat_False()
        {
            // ジャングラーは従来どおり Farm/Push のみ(Retreat では発火させない=挙動不変)。
            Assert.IsFalse(PatrolFreezeLogic.WatchdogEligible(true, BotMacroAction.Retreat, 20f));
        }

        [Test]
        public void Eligible_Laner_RetreatInsideJungle_True()
        {
            // レーナーがジャングル内(r<54)で Retreat 中に詰まったら対象(本修正の核心)。
            Assert.IsTrue(PatrolFreezeLogic.WatchdogEligible(false, BotMacroAction.Retreat, 40f));
        }

        [Test]
        public void Eligible_Laner_RetreatOnLane_False()
        {
            // レーン上・敵ベース前(r>=54)での Retreat は正当な退避なので介入しない。
            Assert.IsFalse(PatrolFreezeLogic.WatchdogEligible(false, BotMacroAction.Retreat, 63f));
        }

        [Test]
        public void Eligible_Laner_FarmInsideJungle_True()
        {
            // Farm/Push での既存挙動は維持(ジャングル内で有効)。
            Assert.IsTrue(PatrolFreezeLogic.WatchdogEligible(false, BotMacroAction.Farm, 40f));
        }

        [Test]
        public void Eligible_Laner_DefendInsideJungle_False()
        {
            // Defend/GroupForObjective 等の意図した挙動は対象外。
            Assert.IsFalse(PatrolFreezeLogic.WatchdogEligible(false, BotMacroAction.Defend, 40f));
            Assert.IsFalse(PatrolFreezeLogic.WatchdogEligible(false, BotMacroAction.GroupForObjective, 40f));
        }

        // ── Retreat 復帰先が自陣側(低 index) ──

        [Test]
        public void RetreatRecovery_BiasesTowardOwnBase()
        {
            // index 0 = 自軍ベース。最寄りより1つ低い index(自陣側)を返す。
            Assert.AreEqual(4, PatrolFreezeLogic.LanerRetreatRecoveryIndex(nearestIndex: 5, waypointCount: 13));
            Assert.Less(
                PatrolFreezeLogic.LanerRetreatRecoveryIndex(5, 13),
                5,
                "復帰先は最寄りより自陣側(低 index)であること");
        }

        [Test]
        public void RetreatRecovery_ClampsAtOwnBaseEnd()
        {
            // 既に自陣端(0)ならそのまま 0。範囲外入力もクランプ。
            Assert.AreEqual(0, PatrolFreezeLogic.LanerRetreatRecoveryIndex(0, 13));
            Assert.AreEqual(0, PatrolFreezeLogic.LanerRetreatRecoveryIndex(1, 13));
            Assert.AreEqual(0, PatrolFreezeLogic.LanerRetreatRecoveryIndex(-3, 13));
            Assert.AreEqual(0, PatrolFreezeLogic.LanerRetreatRecoveryIndex(5, 0));
        }

        // ── 脱出オーバーライドの目標ノード選択(EscapeTargetIndex) ──

        [Test]
        public void EscapeTarget_FirstAttempt_NoRetreat_IsNearest()
        {
            // 初回(attempt=0)・非 Retreat は最寄りノードそのもの。
            Assert.AreEqual(6, PatrolFreezeLogic.EscapeTargetIndex(
                nearestIndex: 6, waypointCount: 13, retreatBias: false, attempt: 0));
        }

        [Test]
        public void EscapeTarget_FirstAttempt_Retreat_BiasesTowardOwnBase()
        {
            // 初回・Retreat は自陣側へ1寄せ(LanerRetreatRecoveryIndex 相当)。
            Assert.AreEqual(5, PatrolFreezeLogic.EscapeTargetIndex(
                nearestIndex: 6, waypointCount: 13, retreatBias: true, attempt: 0));
        }

        [Test]
        public void EscapeTarget_Cycles_TowardOwnBase_OnRepeatedAttempts()
        {
            // 再凍結のたびに目標が自陣方向(index 減少)へずれる=同じ塞がれた目標に固執しない。
            int a0 = PatrolFreezeLogic.EscapeTargetIndex(6, 13, false, 0);
            int a1 = PatrolFreezeLogic.EscapeTargetIndex(6, 13, false, 1);
            int a2 = PatrolFreezeLogic.EscapeTargetIndex(6, 13, false, 2);
            Assert.AreEqual(6, a0);
            Assert.AreEqual(5, a1);
            Assert.AreEqual(4, a2);
            Assert.Greater(a0, a1);
            Assert.Greater(a1, a2);
        }

        [Test]
        public void EscapeTarget_ClampsAtOwnBase_AndHandlesEmpty()
        {
            // 自陣端でクランプ、経路なしは 0。
            Assert.AreEqual(0, PatrolFreezeLogic.EscapeTargetIndex(2, 13, false, 5));
            Assert.AreEqual(0, PatrolFreezeLogic.EscapeTargetIndex(6, 0, false, 0));
        }
    }
}
