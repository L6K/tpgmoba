using NUnit.Framework;
using Enigma.Character;

namespace Enigma.Tests
{
    public sealed class ObjectiveMacroLogicTests
    {
        // ── GroupForObjective: ボスがアクティブなら満HPでも合流(デッドロック是正の本丸) ──

        [Test]
        public void GroupForObjective_BossActive_AbandonsCamp()
        {
            // 満HP(=残HP不問)のボスでもアクティブなら合流する。従来はボス<60%を要求しており
            // 誰も着手できず永久にファームし続けるデッドロックだった。
            Assert.IsTrue(
                ObjectiveMacroLogic.JunglerShouldAbandonCamp(
                    BotMacroAction.GroupForObjective, bossActive: true));
        }

        [Test]
        public void GroupForObjective_BossNotActive_DoesNotAbandonCamp()
        {
            // 集合先のボスが不在/未解決/死亡なら、合流先が無いため巡回・狩りを続ける。
            Assert.IsFalse(
                ObjectiveMacroLogic.JunglerShouldAbandonCamp(
                    BotMacroAction.GroupForObjective, bossActive: false));
        }

        // ── CloseOutSiege: 閉幕プッシュは従来どおり常に合流(ボス有無に依らない) ──

        [Test]
        public void CloseOutSiege_AbandonsCamp_RegardlessOfBoss()
        {
            Assert.IsTrue(
                ObjectiveMacroLogic.JunglerShouldAbandonCamp(
                    BotMacroAction.CloseOutSiege, bossActive: false));
            Assert.IsTrue(
                ObjectiveMacroLogic.JunglerShouldAbandonCamp(
                    BotMacroAction.CloseOutSiege, bossActive: true));
        }

        // ── その他のマクロ(Farm/Push/Retreat/Defend)では合流しない ──

        [Test]
        public void Farm_DoesNotAbandonCamp()
        {
            Assert.IsFalse(
                ObjectiveMacroLogic.JunglerShouldAbandonCamp(BotMacroAction.Farm, bossActive: true));
        }

        [Test]
        public void Push_DoesNotAbandonCamp()
        {
            Assert.IsFalse(
                ObjectiveMacroLogic.JunglerShouldAbandonCamp(BotMacroAction.Push, bossActive: true));
        }

        [Test]
        public void Retreat_DoesNotAbandonCamp()
        {
            // 危険で撤退中はボスがアクティブでも合流しない(離脱を優先)。
            Assert.IsFalse(
                ObjectiveMacroLogic.JunglerShouldAbandonCamp(BotMacroAction.Retreat, bossActive: true));
        }

        [Test]
        public void Defend_DoesNotAbandonCamp()
        {
            Assert.IsFalse(
                ObjectiveMacroLogic.JunglerShouldAbandonCamp(BotMacroAction.Defend, bossActive: true));
        }

        // ── ボス討伐コミット(問題B): 削り途中の離脱→ボス全快リセットの是正 ──

        // BossCommitHpFraction=0.6。0.6 未満のボス残HPを「押し切りライン」とする。
        private const float Below = 0.5f; // < 0.6
        private const float Above = 0.7f; // >= 0.6

        [Test]
        public void BossCommit_Starts_WhenNearAndBossDamagedPastLine()
        {
            // 未コミットでも、至近でボスが押し切りライン未満まで削れていれば着手コミットする。
            Assert.IsTrue(ObjectiveMacroLogic.NextBossCommit(
                alreadyCommitted: false, bossActive: true, bossHpFraction: Below,
                selfCanFight: true, nearObjective: true));
        }

        [Test]
        public void BossCommit_DoesNotStart_WhenBossHpAboveLine()
        {
            // まだ十分削れていない(>=0.6)なら着手コミットしない(全員がやみくもに張り付かない)。
            Assert.IsFalse(ObjectiveMacroLogic.NextBossCommit(
                alreadyCommitted: false, bossActive: true, bossHpFraction: Above,
                selfCanFight: true, nearObjective: true));
        }

        [Test]
        public void BossCommit_DoesNotStart_WhenFarFromBoss()
        {
            // 至近に居ない(交戦していない)なら、ボスが削れていても着手コミットしない。
            Assert.IsFalse(ObjectiveMacroLogic.NextBossCommit(
                alreadyCommitted: false, bossActive: true, bossHpFraction: Below,
                selfCanFight: true, nearObjective: false));
        }

        [Test]
        public void BossCommit_Maintained_EvenWhenPushedOutAndBossHealed()
        {
            // 一度コミットしたら、至近から押し出されてもボスHPが戻っても維持する(マクロ再評価による離脱を止める)。
            Assert.IsTrue(ObjectiveMacroLogic.NextBossCommit(
                alreadyCommitted: true, bossActive: true, bossHpFraction: 1f,
                selfCanFight: true, nearObjective: false));
        }

        [Test]
        public void BossCommit_Released_OnRetreat()
        {
            // 低HP撤退(selfCanFight=false)はコミットより優先: 例外として解除する。
            Assert.IsFalse(ObjectiveMacroLogic.NextBossCommit(
                alreadyCommitted: true, bossActive: true, bossHpFraction: Below,
                selfCanFight: false, nearObjective: true));
        }

        [Test]
        public void BossCommit_Released_WhenBossDies()
        {
            // ボス消滅/死亡(bossActive=false)でコミット解除。
            Assert.IsFalse(ObjectiveMacroLogic.NextBossCommit(
                alreadyCommitted: true, bossActive: false, bossHpFraction: Below,
                selfCanFight: true, nearObjective: true));
        }

        [Test]
        public void ApplyBossCommit_ForcesGroupForObjective_WhenCommitted()
        {
            // コミット中は Decide が Farm/CloseOutSiege/Defend を返しても GroupForObjective に固定する。
            Assert.AreEqual(BotMacroAction.GroupForObjective,
                ObjectiveMacroLogic.ApplyBossCommit(BotMacroAction.Farm, committed: true));
            Assert.AreEqual(BotMacroAction.GroupForObjective,
                ObjectiveMacroLogic.ApplyBossCommit(BotMacroAction.CloseOutSiege, committed: true));
            Assert.AreEqual(BotMacroAction.GroupForObjective,
                ObjectiveMacroLogic.ApplyBossCommit(BotMacroAction.Defend, committed: true));
        }

        [Test]
        public void ApplyBossCommit_PassesThrough_WhenNotCommitted()
        {
            // 非コミット時は Decide の結果をそのまま通す。
            Assert.AreEqual(BotMacroAction.Farm,
                ObjectiveMacroLogic.ApplyBossCommit(BotMacroAction.Farm, committed: false));
            Assert.AreEqual(BotMacroAction.Retreat,
                ObjectiveMacroLogic.ApplyBossCommit(BotMacroAction.Retreat, committed: false));
        }
    }
}
