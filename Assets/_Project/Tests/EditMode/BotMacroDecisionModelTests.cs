using NUnit.Framework;
using Enigma.Character;

namespace Enigma.Tests
{
    public sealed class BotMacroDecisionModelTests
    {
        private static BotMacroContext C(
            float hp = 1f,
            int allies = 3,
            int enemies = 3,
            bool objective = false,
            float objectiveDistance = 999f,
            bool minions = false,
            bool towerThreat = false,
            bool ownTowerUnderAttack = false,
            float distanceToThreatenedTower = float.MaxValue,
            float bossHp = 1f,
            int ownTowersAlive = 0,
            int enemyTowersAlive = 0,
            int ownTowersMax = 0,
            int enemyTowersMax = 0,
            int teamKillLead = 0)
        {
            return new BotMacroContext(
                hp, allies, enemies, objective, objectiveDistance, minions, towerThreat,
                ownTowerUnderAttack, distanceToThreatenedTower,
                bossHp, ownTowersAlive, enemyTowersAlive, ownTowersMax, enemyTowersMax,
                teamKillLead);
        }

        [Test]
        public void LowHp_And_Outnumbered_Retreats()
        {
            var action = BotMacroDecisionModel.Decide(C(hp: 0.2f, allies: 2, enemies: 3));
            Assert.AreEqual(BotMacroAction.Retreat, action);
        }

        [Test]
        public void LowHp_And_EqualNumbers_Retreats()
        {
            var action = BotMacroDecisionModel.Decide(C(hp: 0.2f, allies: 3, enemies: 3));
            Assert.AreEqual(BotMacroAction.Retreat, action);
        }

        [Test]
        public void LowHp_And_TowerThreat_Retreats()
        {
            var action = BotMacroDecisionModel.Decide(
                C(hp: 0.2f, allies: 4, enemies: 2, towerThreat: true));
            Assert.AreEqual(BotMacroAction.Retreat, action);
        }

        [Test]
        public void LowHp_WithAdvantage_AndNoTowerThreat_DoesNotRetreat()
        {
            var action = BotMacroDecisionModel.Decide(C(hp: 0.2f, allies: 4, enemies: 2));
            Assert.AreEqual(BotMacroAction.Farm, action);
        }

        [Test]
        public void AtLowHpThreshold_DoesNotRetreat()
        {
            var action = BotMacroDecisionModel.Decide(
                C(hp: BotMacroDecisionModel.LowHpFraction, allies: 1, enemies: 5));
            Assert.AreEqual(BotMacroAction.Farm, action);
        }

        [Test]
        public void ObjectiveActive_SafeHp_NumbersEven_InRange_Groups()
        {
            var action = BotMacroDecisionModel.Decide(
                C(hp: 0.8f, allies: 3, enemies: 3, objective: true, objectiveDistance: 20f));
            Assert.AreEqual(BotMacroAction.GroupForObjective, action);
        }

        [Test]
        public void ObjectiveActive_AtJoinRange_Groups()
        {
            var action = BotMacroDecisionModel.Decide(
                C(hp: 0.8f, objective: true,
                  objectiveDistance: BotMacroDecisionModel.ObjectiveJoinRange));
            Assert.AreEqual(BotMacroAction.GroupForObjective, action);
        }

        [Test]
        public void ObjectiveActive_OutOfRange_DoesNotGroup()
        {
            var action = BotMacroDecisionModel.Decide(
                C(hp: 0.8f, objective: true,
                  objectiveDistance: BotMacroDecisionModel.ObjectiveJoinRange + 0.1f));
            Assert.AreEqual(BotMacroAction.Farm, action);
        }

        [Test]
        public void ObjectiveActive_Outnumbered_DoesNotGroup()
        {
            var action = BotMacroDecisionModel.Decide(
                C(hp: 0.8f, allies: 2, enemies: 3, objective: true, objectiveDistance: 20f));
            Assert.AreEqual(BotMacroAction.Farm, action);
        }

        [Test]
        public void ObjectiveActive_BelowSafeHp_DoesNotGroup()
        {
            var action = BotMacroDecisionModel.Decide(
                C(hp: BotMacroDecisionModel.SafeHpFraction - 0.01f,
                  objective: true, objectiveDistance: 20f));
            Assert.AreEqual(BotMacroAction.Farm, action);
        }

        [Test]
        public void AllyAdvantage_SafeHp_WithMinions_Pushes()
        {
            var action = BotMacroDecisionModel.Decide(
                C(hp: 0.8f, allies: 4, enemies: 2, minions: true));
            Assert.AreEqual(BotMacroAction.Push, action);
        }

        [Test]
        public void AllyAdvantage_AtSafeHp_WithMinions_Pushes()
        {
            var action = BotMacroDecisionModel.Decide(
                C(hp: BotMacroDecisionModel.SafeHpFraction, allies: 4, enemies: 2, minions: true));
            Assert.AreEqual(BotMacroAction.Push, action);
        }

        [Test]
        public void AllyAdvantage_WithoutMinions_DoesNotPush()
        {
            var action = BotMacroDecisionModel.Decide(C(hp: 0.8f, allies: 4, enemies: 2));
            Assert.AreEqual(BotMacroAction.Farm, action);
        }

        [Test]
        public void TowerThreat_WithoutMinions_Defends()
        {
            var action = BotMacroDecisionModel.Decide(C(hp: 0.8f, towerThreat: true));
            Assert.AreEqual(BotMacroAction.Defend, action);
        }

        [Test]
        public void TowerThreat_WithMinions_DoesNotDefend()
        {
            var action = BotMacroDecisionModel.Decide(
                C(hp: 0.8f, minions: true, towerThreat: true));
            Assert.AreEqual(BotMacroAction.Farm, action);
        }

        [Test]
        public void NoRuleMatches_Farms()
        {
            var action = BotMacroDecisionModel.Decide(C(hp: 0.8f, allies: 3, enemies: 3));
            Assert.AreEqual(BotMacroAction.Farm, action);
        }

        [Test]
        public void Retreat_HasPriorityOverPush()
        {
            var action = BotMacroDecisionModel.Decide(
                C(hp: 0.2f, allies: 4, enemies: 2, minions: true, towerThreat: true));
            Assert.AreEqual(BotMacroAction.Retreat, action);
        }

        [Test]
        public void Objective_HasPriorityOverPush()
        {
            var action = BotMacroDecisionModel.Decide(
                C(hp: 0.8f, allies: 4, enemies: 2,
                  objective: true, objectiveDistance: 20f, minions: true));
            Assert.AreEqual(BotMacroAction.GroupForObjective, action);
        }

        [Test]
        public void OwnTowerUnderAttack_SafeHp_InRange_Defends()
        {
            var action = BotMacroDecisionModel.Decide(
                C(hp: 0.8f, allies: 3, enemies: 3,
                  ownTowerUnderAttack: true, distanceToThreatenedTower: 20f));
            Assert.AreEqual(BotMacroAction.Defend, action);
        }

        [Test]
        public void OwnTowerUnderAttack_LowHp_Outnumbered_RetreatsInstead()
        {
            var action = BotMacroDecisionModel.Decide(
                C(hp: 0.3f, allies: 2, enemies: 4,
                  ownTowerUnderAttack: true, distanceToThreatenedTower: 20f));
            Assert.AreEqual(BotMacroAction.Retreat, action);
        }

        [Test]
        public void OwnTowerUnderAttack_OutOfDefendRange_DoesNotDefend()
        {
            // 圏外は定数から導出する(M-0 のマップ1.4倍で 45→63 に変わりリテラル50が圏内化した回帰の再発防止)
            var action = BotMacroDecisionModel.Decide(
                C(hp: 0.8f, allies: 3, enemies: 3,
                  ownTowerUnderAttack: true,
                  distanceToThreatenedTower: BotMacroDecisionModel.DefendJoinRange + 5f));
            Assert.AreEqual(BotMacroAction.Farm, action);
        }

        [Test]
        public void OwnTowerUnderAttack_AndObjectiveActive_DefendsBeforeGrouping()
        {
            var action = BotMacroDecisionModel.Decide(
                C(hp: 0.8f, allies: 3, enemies: 3,
                  objective: true, objectiveDistance: 10f,
                  ownTowerUnderAttack: true, distanceToThreatenedTower: 20f));
            Assert.AreEqual(BotMacroAction.Defend, action);
        }

        // ── CloseOutSiege（閉幕プッシュ）── 自チーム優勢判定・優先順位 ──────────────

        [Test]
        public void TeamAhead_ByExactlyOneTower_SafeHp_ClosesOutSiege()
        {
            // enemyLost(2) - ownLost(1) = 1 = CloseOutTowerAdvantage(境界ちょうど)
            var action = BotMacroDecisionModel.Decide(
                C(hp: 0.8f, allies: 3, enemies: 3,
                  ownTowersAlive: 2, ownTowersMax: 3,
                  enemyTowersAlive: 1, enemyTowersMax: 3));
            Assert.AreEqual(BotMacroAction.CloseOutSiege, action);
        }

        [Test]
        public void TeamAhead_ByOneLessThanAdvantage_DoesNotCloseOutSiege()
        {
            // enemyLost(1) - ownLost(1) = 0 < CloseOutTowerAdvantage(1) → 押し切り判定なし
            var action = BotMacroDecisionModel.Decide(
                C(hp: 0.8f, allies: 3, enemies: 3,
                  ownTowersAlive: 2, ownTowersMax: 3,
                  enemyTowersAlive: 2, enemyTowersMax: 3));
            Assert.AreEqual(BotMacroAction.Farm, action);
        }

        [Test]
        public void TeamBehind_DoesNotCloseOutSiege()
        {
            var action = BotMacroDecisionModel.Decide(
                C(hp: 0.8f, allies: 3, enemies: 3,
                  ownTowersAlive: 1, ownTowersMax: 3,
                  enemyTowersAlive: 3, enemyTowersMax: 3));
            Assert.AreEqual(BotMacroAction.Farm, action);
        }

        [Test]
        public void TeamAhead_BelowSafeHp_DoesNotCloseOutSiege()
        {
            var action = BotMacroDecisionModel.Decide(
                C(hp: SafeHpBelow, allies: 3, enemies: 3,
                  ownTowersAlive: 3, ownTowersMax: 3,
                  enemyTowersAlive: 0, enemyTowersMax: 3));
            Assert.AreEqual(BotMacroAction.Farm, action);
        }

        [Test]
        public void NoTowerDataSupplied_DoesNotCloseOutSiege()
        {
            // OwnTowersMax/EnemyTowersMax とも既定の0(未供給)なら判定不能として Farm へフォールバック
            var action = BotMacroDecisionModel.Decide(C(hp: 0.8f, allies: 3, enemies: 3));
            Assert.AreEqual(BotMacroAction.Farm, action);
        }

        [Test]
        public void Retreat_HasPriorityOverCloseOutSiege()
        {
            var action = BotMacroDecisionModel.Decide(
                C(hp: 0.2f, allies: 2, enemies: 3,
                  ownTowersAlive: 3, ownTowersMax: 3,
                  enemyTowersAlive: 0, enemyTowersMax: 3));
            Assert.AreEqual(BotMacroAction.Retreat, action);
        }

        [Test]
        public void Defend_HasPriorityOverCloseOutSiege()
        {
            var action = BotMacroDecisionModel.Decide(
                C(hp: 0.8f, allies: 3, enemies: 3,
                  ownTowerUnderAttack: true, distanceToThreatenedTower: 20f,
                  ownTowersAlive: 3, ownTowersMax: 3,
                  enemyTowersAlive: 0, enemyTowersMax: 3));
            Assert.AreEqual(BotMacroAction.Defend, action);
        }

        [Test]
        public void GroupForObjective_HasPriorityOverCloseOutSiege()
        {
            var action = BotMacroDecisionModel.Decide(
                C(hp: 0.8f, allies: 3, enemies: 3,
                  objective: true, objectiveDistance: 20f,
                  ownTowersAlive: 3, ownTowersMax: 3,
                  enemyTowersAlive: 0, enemyTowersMax: 3));
            Assert.AreEqual(BotMacroAction.GroupForObjective, action);
        }

        [Test]
        public void CloseOutSiege_HasPriorityOverPush()
        {
            // Push の条件(allies>enemies かつ minions)も同時に満たすが、優勢判定が優先される
            var action = BotMacroDecisionModel.Decide(
                C(hp: 0.8f, allies: 4, enemies: 2, minions: true,
                  ownTowersAlive: 3, ownTowersMax: 3,
                  enemyTowersAlive: 0, enemyTowersMax: 3));
            Assert.AreEqual(BotMacroAction.CloseOutSiege, action);
        }

        private const float SafeHpBelow = BotMacroDecisionModel.SafeHpFraction - 0.01f;

        // ── ボス討伐コミットライン（BossCommitHpFraction）境界値 ──────────────
        // Decide 自体は BossHpFraction を分岐に使わない(コミット判定は ApplyMacroOverride 側の
        // 責務)。ここでは定数の境界値そのものを検証し、回帰(値のドリフト)を防ぐ。

        [Test]
        public void BossCommitHpFraction_IsAboveObservedDisengageRatio()
        {
            // 実測 1000→533 離脱(0.533)より高い値でなければコミットが早すぎず効果が出ない
            const float observedDisengageRatio = 533f / 1000f;
            Assert.Greater(BotMacroDecisionModel.BossCommitHpFraction, observedDisengageRatio);
        }

        [Test]
        public void BossCommitHpFraction_IsBelowFull()
        {
            // 1 以上だと開幕から常時コミット扱いになり敵接近の離脱判定自体が無意味になる
            Assert.Less(BotMacroDecisionModel.BossCommitHpFraction, 1f);
        }

        // ── キル差による閉幕トリガー（TeamKillLead）── タワー損失差が発生しない試合への対策 ──

        [Test]
        public void KillLead_BelowThreshold_DoesNotCloseOutSiege()
        {
            // キル差2 < CloseOutKillLead(3)、タワー差も無し → 押し切り判定なし
            var action = BotMacroDecisionModel.Decide(
                C(hp: 0.8f, allies: 3, enemies: 3, teamKillLead: 2));
            Assert.AreEqual(BotMacroAction.Farm, action);
        }

        [Test]
        public void KillLead_AtThreshold_ClosesOutSiege()
        {
            // キル差3 = CloseOutKillLead(3)、タワー差は無しでも成立する
            var action = BotMacroDecisionModel.Decide(
                C(hp: 0.8f, allies: 3, enemies: 3, teamKillLead: 3));
            Assert.AreEqual(BotMacroAction.CloseOutSiege, action);
        }

        [Test]
        public void KillLead_OrTowerAdvantage_EitherAloneIsSufficient()
        {
            // タワー差だけ(キル差0)でも従来どおり成立する(OR条件の一方のみでも十分)
            var action = BotMacroDecisionModel.Decide(
                C(hp: 0.8f, allies: 3, enemies: 3, teamKillLead: 0,
                  ownTowersAlive: 2, ownTowersMax: 3,
                  enemyTowersAlive: 1, enemyTowersMax: 3));
            Assert.AreEqual(BotMacroAction.CloseOutSiege, action);
        }

        [Test]
        public void KillLead_BelowSafeHp_DoesNotCloseOutSiege()
        {
            var action = BotMacroDecisionModel.Decide(
                C(hp: SafeHpBelow, allies: 3, enemies: 3, teamKillLead: 5));
            Assert.AreEqual(BotMacroAction.Farm, action);
        }

        [Test]
        public void KillLead_Retreat_HasPriorityOverCloseOutSiege()
        {
            var action = BotMacroDecisionModel.Decide(
                C(hp: 0.2f, allies: 2, enemies: 3, teamKillLead: 10));
            Assert.AreEqual(BotMacroAction.Retreat, action);
        }

        [Test]
        public void KillLead_Defend_HasPriorityOverCloseOutSiege()
        {
            var action = BotMacroDecisionModel.Decide(
                C(hp: 0.8f, allies: 3, enemies: 3, teamKillLead: 10,
                  ownTowerUnderAttack: true, distanceToThreatenedTower: 20f));
            Assert.AreEqual(BotMacroAction.Defend, action);
        }

        [Test]
        public void OmittedTeamKillLead_DefaultsToZero_BackwardCompatible()
        {
            // 新引数を省略した旧呼び出し（デフォルト値0）でも従来どおりタワー差判定のみで動く
            var ctx = new BotMacroContext(
                selfHpFraction: 0.8f, alliesAlive: 3, enemiesAlive: 3,
                objectiveActiveOrSoon: false, distanceToObjective: 999f,
                alliedMinionsPresent: false, underTowerThreat: false,
                ownTowerUnderAttack: false, distanceToThreatenedTower: float.MaxValue,
                bossHpFraction: 1f,
                ownTowersAlive: 2, enemyTowersAlive: 1,
                ownTowersMax: 3, enemyTowersMax: 3);

            var action = BotMacroDecisionModel.Decide(in ctx);
            Assert.AreEqual(BotMacroAction.CloseOutSiege, action);
        }
    }
}
