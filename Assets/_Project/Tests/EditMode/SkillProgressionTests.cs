using NUnit.Framework;
using System;
using Enigma.Abilities;

namespace Enigma.Tests
{
    public sealed class SkillProgressionTests
    {
        // ---- Initial state ----

        [Test]
        public void Initial_UnspentPoints_IsOne()
        {
            var sp = new SkillProgression();
            Assert.AreEqual(1, sp.UnspentPoints);
        }

        [Test]
        public void Initial_AllRanks_AreZero()
        {
            var sp = new SkillProgression();
            Assert.AreEqual(0, sp.GetRank(0));
            Assert.AreEqual(0, sp.GetRank(1));
            Assert.AreEqual(0, sp.GetRank(2));
        }

        // ---- Q at level 1 ----

        [Test]
        public void CanLevelUp_Q_AtLevel1_IsTrue()
        {
            var sp = new SkillProgression();
            Assert.IsTrue(sp.CanLevelUp(0, 1));
        }

        [Test]
        public void TryLevelUp_Q_AtLevel1_RankBecomesOne()
        {
            var sp = new SkillProgression();
            sp.TryLevelUp(0, 1);
            Assert.AreEqual(1, sp.GetRank(0));
            Assert.AreEqual(0, sp.UnspentPoints);
        }

        // ---- R cannot be taken before level 6 ----

        [Test]
        public void CanLevelUp_R_AtLevel1Through5_IsFalse()
        {
            var sp = new SkillProgression();
            for (int lv = 1; lv <= 5; lv++)
                Assert.IsFalse(sp.CanLevelUp(2, lv), $"R should not be available at level {lv}");
        }

        // ---- Q rank 2 level gate ----

        [Test]
        public void CanLevelUp_QRank2_AtLevel2_IsFalse()
        {
            var sp = new SkillProgression();
            sp.TryLevelUp(0, 1);           // rank 1 acquired
            sp.OnChampionLevelUp();        // lv2 → gain 1 point
            Assert.IsFalse(sp.CanLevelUp(0, 2));
        }

        [Test]
        public void CanLevelUp_QRank2_AtLevel3_IsTrue()
        {
            var sp = new SkillProgression();
            sp.TryLevelUp(0, 1);           // rank 1
            sp.OnChampionLevelUp();        // lv2
            sp.OnChampionLevelUp();        // lv3, but we track points not level here
            Assert.IsTrue(sp.CanLevelUp(0, 3));
        }

        // ---- R level gates: 6/8/10 ----

        [Test]
        public void CanLevelUp_RRank1_AtLevel6_IsTrue()
        {
            var sp = new SkillProgression();
            // Give enough points by simulating level-ups
            for (int i = 0; i < 5; i++) sp.OnChampionLevelUp(); // total 6 points
            sp.TryLevelUp(0, 9); // spend some elsewhere so we have 1 left for R
            sp.TryLevelUp(0, 9);
            sp.TryLevelUp(0, 9);
            sp.TryLevelUp(1, 9);
            sp.TryLevelUp(1, 9);
            // 1 point remains
            Assert.AreEqual(1, sp.UnspentPoints);
            Assert.IsTrue(sp.CanLevelUp(2, 6));
        }

        [Test]
        public void CanLevelUp_RRank1_AtLevel5_IsFalse()
        {
            var sp = new SkillProgression();
            Assert.IsFalse(sp.CanLevelUp(2, 5));
        }

        [Test]
        public void CanLevelUp_RRank2_AtLevel7_IsFalse()
        {
            var sp = new SkillProgression();
            // Reach rank 1 of R first
            for (int i = 0; i < 5; i++) sp.OnChampionLevelUp();
            sp.TryLevelUp(0, 9);
            sp.TryLevelUp(0, 9);
            sp.TryLevelUp(0, 9);
            sp.TryLevelUp(1, 9);
            sp.TryLevelUp(1, 9);
            sp.TryLevelUp(2, 6);           // R rank 1
            sp.OnChampionLevelUp();        // 1 more point
            Assert.IsFalse(sp.CanLevelUp(2, 7));
        }

        [Test]
        public void CanLevelUp_RRank2_AtLevel8_IsTrue()
        {
            var sp = new SkillProgression();
            for (int i = 0; i < 5; i++) sp.OnChampionLevelUp();
            sp.TryLevelUp(0, 9);
            sp.TryLevelUp(0, 9);
            sp.TryLevelUp(0, 9);
            sp.TryLevelUp(1, 9);
            sp.TryLevelUp(1, 9);
            sp.TryLevelUp(2, 6);           // R rank 1
            sp.OnChampionLevelUp();
            Assert.IsTrue(sp.CanLevelUp(2, 8));
        }

        [Test]
        public void CanLevelUp_RRank3_AtLevel10_IsTrue()
        {
            var sp = new SkillProgression();
            for (int i = 0; i < 8; i++) sp.OnChampionLevelUp(); // 9 total points
            sp.TryLevelUp(0, 9);
            sp.TryLevelUp(0, 9);
            sp.TryLevelUp(0, 9);
            sp.TryLevelUp(1, 9);
            sp.TryLevelUp(1, 9);
            sp.TryLevelUp(1, 9);
            sp.TryLevelUp(2, 6);           // R rank 1
            sp.TryLevelUp(2, 8);           // R rank 2
            // 1 point remains
            Assert.AreEqual(1, sp.UnspentPoints);
            Assert.IsTrue(sp.CanLevelUp(2, 10));
        }

        // ---- Zero unspent points blocks everything ----

        [Test]
        public void CanLevelUp_WithNoPoints_IsFalse()
        {
            var sp = new SkillProgression();
            sp.TryLevelUp(0, 1);           // spend the 1 starting point
            Assert.AreEqual(0, sp.UnspentPoints);
            Assert.IsFalse(sp.CanLevelUp(0, 9));
            Assert.IsFalse(sp.CanLevelUp(1, 9));
            Assert.IsFalse(sp.CanLevelUp(2, 10));
        }

        // ---- Max rank caps ----

        [Test]
        public void CanLevelUp_Q_AtMaxRank5_IsFalse()
        {
            var sp = new SkillProgression();
            for (int i = 0; i < 4; i++) sp.OnChampionLevelUp(); // total 5 points
            sp.TryLevelUp(0, 1);
            sp.TryLevelUp(0, 3);
            sp.TryLevelUp(0, 5);
            sp.TryLevelUp(0, 7);
            sp.TryLevelUp(0, 9);
            Assert.AreEqual(5, sp.GetRank(0));
            Assert.AreEqual(0, sp.UnspentPoints);
            sp.OnChampionLevelUp();
            Assert.IsFalse(sp.CanLevelUp(0, 10));
        }

        [Test]
        public void CanLevelUp_R_AtMaxRank3_IsFalse()
        {
            var sp = new SkillProgression();
            for (int i = 0; i < 8; i++) sp.OnChampionLevelUp(); // 9 total
            sp.TryLevelUp(0, 9);
            sp.TryLevelUp(0, 9);
            sp.TryLevelUp(0, 9);
            sp.TryLevelUp(1, 9);
            sp.TryLevelUp(1, 9);
            sp.TryLevelUp(1, 9);
            sp.TryLevelUp(2, 6);
            sp.TryLevelUp(2, 8);
            sp.TryLevelUp(2, 10);
            Assert.AreEqual(3, sp.GetRank(2));
            sp.OnChampionLevelUp();
            Assert.IsFalse(sp.CanLevelUp(2, 10));
        }

        // ---- DamageMultiplier ----

        [Test]
        public void DamageMultiplier_Rank0_IsZero()
        {
            Assert.AreEqual(0f, SkillProgression.DamageMultiplier(0), 0.001f);
        }

        [Test]
        public void DamageMultiplier_NegativeRank_IsZero()
        {
            Assert.AreEqual(0f, SkillProgression.DamageMultiplier(-1), 0.001f);
        }

        [Test]
        public void DamageMultiplier_Rank1_IsOne()
        {
            Assert.AreEqual(1f, SkillProgression.DamageMultiplier(1), 0.001f);
        }

        [Test]
        public void DamageMultiplier_Rank3_Is1Point5()
        {
            Assert.AreEqual(1.5f, SkillProgression.DamageMultiplier(3), 0.001f);
        }

        // ---- Invalid slot throws ----

        [Test]
        public void GetRank_InvalidSlot_ThrowsArgumentOutOfRange()
        {
            var sp = new SkillProgression();
            Assert.Throws<ArgumentOutOfRangeException>(() => sp.GetRank(3));
            Assert.Throws<ArgumentOutOfRangeException>(() => sp.GetRank(-1));
        }

        [Test]
        public void CanLevelUp_InvalidSlot_ThrowsArgumentOutOfRange()
        {
            var sp = new SkillProgression();
            Assert.Throws<ArgumentOutOfRangeException>(() => sp.CanLevelUp(5, 1));
        }

        [Test]
        public void TryLevelUp_InvalidSlot_ThrowsArgumentOutOfRange()
        {
            var sp = new SkillProgression();
            Assert.Throws<ArgumentOutOfRangeException>(() => sp.TryLevelUp(-1, 1));
        }

        // ---- Changed event fires ----

        [Test]
        public void TryLevelUp_Success_FiresChangedEvent()
        {
            var sp = new SkillProgression();
            int fired = 0;
            sp.Changed += () => fired++;
            sp.TryLevelUp(0, 1);
            Assert.AreEqual(1, fired);
        }

        [Test]
        public void OnChampionLevelUp_FiresChangedEvent()
        {
            var sp = new SkillProgression();
            int fired = 0;
            sp.Changed += () => fired++;
            sp.OnChampionLevelUp();
            Assert.AreEqual(1, fired);
        }

        [Test]
        public void TryLevelUp_Failure_DoesNotFireChangedEvent()
        {
            var sp = new SkillProgression();
            sp.TryLevelUp(0, 1); // spend only point
            int fired = 0;
            sp.Changed += () => fired++;
            sp.TryLevelUp(0, 9); // no points left
            Assert.AreEqual(0, fired);
        }
    }
}
