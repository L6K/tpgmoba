using NUnit.Framework;
using Enigma.Combat;

namespace Enigma.Tests.EditMode
{
    public sealed class MultiKillStreakModelTests
    {
        [Test]
        public void MultiKill_WithinWindow_EscalatesAndClampsAtPenta()
        {
            var m = new MultiKillStreakModel(multiKillWindowSeconds: 10f);
            Assert.AreEqual(MultiKill.None,   m.RegisterKill("A", "v1", 0f).MultiKill);
            Assert.AreEqual(MultiKill.Double, m.RegisterKill("A", "v2", 1f).MultiKill);
            Assert.AreEqual(MultiKill.Triple, m.RegisterKill("A", "v3", 2f).MultiKill);
            Assert.AreEqual(MultiKill.Quadra, m.RegisterKill("A", "v4", 3f).MultiKill);
            Assert.AreEqual(MultiKill.Penta,  m.RegisterKill("A", "v5", 4f).MultiKill);
            var sixth = m.RegisterKill("A", "v6", 5f);
            Assert.AreEqual(MultiKill.Penta, sixth.MultiKill);
            Assert.AreEqual(6, sixth.MultiKillCount);
        }

        [Test]
        public void MultiKill_BeyondWindow_ResetsToNone()
        {
            var m = new MultiKillStreakModel(multiKillWindowSeconds: 10f);
            m.RegisterKill("A", "v1", 0f);
            var second = m.RegisterKill("A", "v2", 1f); // Double
            Assert.AreEqual(MultiKill.Double, second.MultiKill);
            var late = m.RegisterKill("A", "v3", 20f); // 窓超過 → None(=1)
            Assert.AreEqual(MultiKill.None, late.MultiKill);
            Assert.AreEqual(1, late.MultiKillCount);
        }

        [Test]
        public void Streak_Tiers()
        {
            var m = new MultiKillStreakModel();
            Streak last = Streak.None;
            for (int i = 1; i <= 11; i++)
                last = m.RegisterKill("A", "v" + i, i).Streak;
            // 11連続 → Godlike。途中段階も確認。
            Assert.AreEqual(Streak.Godlike, last);

            var m2 = new MultiKillStreakModel();
            Assert.AreEqual(Streak.None,  Streak3(m2, 2)); // 2連続
            Assert.AreEqual(Streak.Spree, Streak3(m2, 1)); // 3連続目
        }

        // 連続で n 回キルして最後の段階を返す（同一killerに victim を変えて）
        private static Streak Streak3(MultiKillStreakModel m, int more)
        {
            Streak s = Streak.None;
            for (int i = 0; i < more; i++) s = m.RegisterKill("K", "x" + System.Guid.NewGuid(), 1f).Streak;
            return s;
        }

        [Test]
        public void Shutdown_EndsVictimStreak()
        {
            var m = new MultiKillStreakModel();
            // B が Spree(3連続)になる
            m.RegisterKill("B", "a1", 0f);
            m.RegisterKill("B", "a2", 1f);
            m.RegisterKill("B", "a3", 2f);
            Assert.AreEqual(3, m.StreakCountOf("B"));
            // A が B を倒す → シャットダウン
            var r = m.RegisterKill("A", "B", 3f);
            Assert.IsTrue(r.IsShutdown);
            Assert.AreEqual(Streak.Spree, r.VictimStreakEnded);
            Assert.AreEqual(0, m.StreakCountOf("B"));
        }

        [Test]
        public void RegisterDeath_ResetsStreakAndWindow()
        {
            var m = new MultiKillStreakModel();
            m.RegisterKill("A", "v1", 0f);
            m.RegisterKill("A", "v2", 1f); // Double, streak 2
            m.RegisterDeath("A", 2f);
            Assert.AreEqual(0, m.StreakCountOf("A"));
            var next = m.RegisterKill("A", "v3", 3f);
            Assert.AreEqual(MultiKill.None, next.MultiKill); // 窓リセット
            Assert.AreEqual(1, next.StreakCount);            // ストリークリセット後の1
        }

        [Test]
        public void SelfKill_Ignored_And_NullIdsBecomeUnknown()
        {
            var m = new MultiKillStreakModel();
            var self = m.RegisterKill("A", "A", 0f);
            Assert.AreEqual(MultiKill.None, self.MultiKill);
            Assert.AreEqual(0, self.MultiKillCount);

            // null killer/victim は "Unknown" に集約され、Unknown==Unknown は自殺扱いで無視される
            var nn = m.RegisterKill(null, null, 0f);
            Assert.AreEqual(0, nn.MultiKillCount);
            // 通常の Unknown キル(victim 指定)は成立
            var u = m.RegisterKill(null, "v", 1f);
            Assert.AreEqual(1, u.MultiKillCount);
        }
    }
}
