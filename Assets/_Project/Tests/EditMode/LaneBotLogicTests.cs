using NUnit.Framework;
using Enigma.Character;

namespace Enigma.Tests
{
    public sealed class LaneBotLogicTests
    {
        // --- ヘルパー: よく使う知覚を簡潔に組み立てる ---
        private static LaneBotPerception P(
            float hp                = 1f,
            float nearDist          = 999f,
            LaneThreatKind nearKind = LaneThreatKind.None,
            bool attacker           = false,
            float attackerDist      = 999f,
            float towerDist         = float.MaxValue,
            bool allyMinion         = false)
        {
            return new LaneBotPerception(
                hp, nearDist, nearKind, attacker, attackerDist, towerDist, allyMinion);
        }

        // --- Push 状態 ---

        [Test]
        public void Push_NoEnemy_MovesForward()
        {
            var d = LaneBotLogic.Decide(LaneBotState.Push, P());
            Assert.AreEqual(LaneBotState.Push, d.State);
            Assert.AreEqual(LaneMove.Forward, d.Move);
            Assert.IsFalse(d.HasAttackTarget);
        }

        [Test]
        public void Push_EnemyEntersAggro_TransitionsToEngage()
        {
            // aggroRange(14)内に敵ミニオン → Engage へ
            var d = LaneBotLogic.Decide(LaneBotState.Push,
                P(nearDist: 13f, nearKind: LaneThreatKind.Minion));
            Assert.AreEqual(LaneBotState.Engage, d.State);
        }

        [Test]
        public void Push_EnemyOutsideAggro_StaysPush()
        {
            var d = LaneBotLogic.Decide(LaneBotState.Push,
                P(nearDist: 14.5f, nearKind: LaneThreatKind.Minion));
            Assert.AreEqual(LaneBotState.Push, d.State);
            Assert.AreEqual(LaneMove.Forward, d.Move);
        }

        // --- Engage 状態: 接近・攻撃 ---

        [Test]
        public void Engage_TargetInAttackRange_StopsAndAttacks()
        {
            // attackRange(11)内 → 停止して攻撃
            var d = LaneBotLogic.Decide(LaneBotState.Engage,
                P(nearDist: 10f, nearKind: LaneThreatKind.Minion));
            Assert.AreEqual(LaneBotState.Engage, d.State);
            Assert.AreEqual(LaneMove.Stop, d.Move);
            Assert.IsTrue(d.HasAttackTarget);
        }

        [Test]
        public void Engage_TargetOutsideAttackRangeButInAggro_MovesForward()
        {
            // aggro(14)内・attack(11)外 → 接近
            var d = LaneBotLogic.Decide(LaneBotState.Engage,
                P(nearDist: 12.5f, nearKind: LaneThreatKind.Minion));
            Assert.AreEqual(LaneBotState.Engage, d.State);
            Assert.AreEqual(LaneMove.Forward, d.Move);
            Assert.IsFalse(d.HasAttackTarget);
        }

        [Test]
        public void Engage_TargetLeavesAggro_ReturnsToPush()
        {
            var d = LaneBotLogic.Decide(LaneBotState.Engage,
                P(nearDist: 20f, nearKind: LaneThreatKind.Minion));
            Assert.AreEqual(LaneBotState.Push, d.State);
            Assert.AreEqual(LaneMove.Forward, d.Move);
        }

        // --- ターゲット優先: 攻撃してきたチャンピオン > 最寄り ---

        [Test]
        public void Engage_AttackerChampionPreferredOverNearest()
        {
            // 最寄りは遠いミニオン、攻撃者チャンピオンは射程内 → 攻撃者を攻撃
            var d = LaneBotLogic.Decide(LaneBotState.Engage,
                P(nearDist: 13f, nearKind: LaneThreatKind.Minion,
                  attacker: true, attackerDist: 9f));
            Assert.IsTrue(d.HasAttackTarget);
            Assert.IsTrue(d.TargetIsAttackerChampion);
            Assert.AreEqual(LaneMove.Stop, d.Move);
        }

        [Test]
        public void Engage_AttackerChampionOutOfAttackRange_Approaches()
        {
            // 攻撃者チャンピオンが aggro 内・attack 外 → 接近（攻撃者を追う）
            var d = LaneBotLogic.Decide(LaneBotState.Engage,
                P(nearDist: 5f, nearKind: LaneThreatKind.Minion,
                  attacker: true, attackerDist: 13f));
            Assert.AreEqual(LaneMove.Forward, d.Move);
            Assert.IsTrue(d.TargetIsAttackerChampion);
            Assert.IsFalse(d.HasAttackTarget);
        }

        [Test]
        public void Engage_NoAttacker_UsesNearestEnemy()
        {
            var d = LaneBotLogic.Decide(LaneBotState.Engage,
                P(nearDist: 8f, nearKind: LaneThreatKind.Champion));
            Assert.IsTrue(d.HasAttackTarget);
            Assert.IsFalse(d.TargetIsAttackerChampion);
        }

        // --- タワーゾーン規律 ---

        [Test]
        public void Engage_InsideTowerZoneNoAllyMinion_BacksOut()
        {
            // 敵タワー範囲(12)内・味方ミニオン不在 → 後退して出る(被弾タンク防止)
            var d = LaneBotLogic.Decide(LaneBotState.Engage,
                P(nearDist: 6f, nearKind: LaneThreatKind.Champion,
                  towerDist: 11f, allyMinion: false));
            Assert.AreEqual(LaneBotState.Push, d.State);
            Assert.AreEqual(LaneMove.Backward, d.Move);
            Assert.IsFalse(d.HasAttackTarget);
        }

        [Test]
        public void Push_AtTowerZoneEdgeNoAllyMinion_Holds()
        {
            // ゾーン縁(12〜14)では前進せず待機(進入防止)
            var d = LaneBotLogic.Decide(LaneBotState.Push,
                P(towerDist: 13f, allyMinion: false));
            Assert.AreEqual(LaneBotState.Push, d.State);
            Assert.AreEqual(LaneMove.Stop, d.Move);
        }

        [Test]
        public void Engage_TowerZoneWithAllyMinion_AllowsDive()
        {
            // 味方ミニオンが近くにいればタワーダイブ許可 → 通常交戦
            var d = LaneBotLogic.Decide(LaneBotState.Engage,
                P(nearDist: 6f, nearKind: LaneThreatKind.Champion,
                  towerDist: 11f, allyMinion: true));
            Assert.AreEqual(LaneBotState.Engage, d.State);
            Assert.IsTrue(d.HasAttackTarget);
        }

        [Test]
        public void Engage_TargetInRangeOutsideTowerZone_AttacksNormally()
        {
            // タワーは範囲外(>12) → ゾーン制約なし
            var d = LaneBotLogic.Decide(LaneBotState.Engage,
                P(nearDist: 8f, nearKind: LaneThreatKind.Champion,
                  towerDist: 30f, allyMinion: false));
            Assert.AreEqual(LaneBotState.Engage, d.State);
            Assert.IsTrue(d.HasAttackTarget);
        }

        // --- Retreat 遷移 ---

        [Test]
        public void Push_LowHp_TransitionsToRetreat()
        {
            // HP比率 < 0.3 → Retreat
            var d = LaneBotLogic.Decide(LaneBotState.Push,
                P(hp: 0.25f, nearDist: 5f, nearKind: LaneThreatKind.Champion));
            Assert.AreEqual(LaneBotState.Retreat, d.State);
            Assert.AreEqual(LaneMove.Backward, d.Move);
        }

        [Test]
        public void Engage_LowHp_TransitionsToRetreat()
        {
            // 交戦中でも低HPなら撤退が割り込む
            var d = LaneBotLogic.Decide(LaneBotState.Engage,
                P(hp: 0.1f, nearDist: 5f, nearKind: LaneThreatKind.Champion));
            Assert.AreEqual(LaneBotState.Retreat, d.State);
        }

        [Test]
        public void Retreat_NotYetRecovered_KeepsRetreating()
        {
            // HP比率 0.9（< 0.95）→ Retreat 継続
            var d = LaneBotLogic.Decide(LaneBotState.Retreat, P(hp: 0.9f));
            Assert.AreEqual(LaneBotState.Retreat, d.State);
            Assert.AreEqual(LaneMove.Backward, d.Move);
        }

        [Test]
        public void Retreat_Recovered_ReturnsToPush()
        {
            // HP比率 > 0.95 → Push へ復帰
            var d = LaneBotLogic.Decide(LaneBotState.Retreat, P(hp: 0.98f));
            Assert.AreEqual(LaneBotState.Push, d.State);
            Assert.AreEqual(LaneMove.Forward, d.Move);
        }

        [Test]
        public void Retreat_AtRecoverThreshold_StaysRetreat()
        {
            // 境界値: ちょうど 0.95 は「> 0.95」を満たさないため Retreat 維持
            var d = LaneBotLogic.Decide(LaneBotState.Retreat, P(hp: 0.95f));
            Assert.AreEqual(LaneBotState.Retreat, d.State);
        }
    }
}
