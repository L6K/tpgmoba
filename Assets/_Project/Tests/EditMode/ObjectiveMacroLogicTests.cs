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
    }
}
