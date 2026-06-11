using NUnit.Framework;
using Enigma.Combat;

namespace Enigma.Tests
{
    public sealed class ExperienceModelTests
    {
        [Test]
        public void InitialLevel_IsOne()
        {
            var model = new ExperienceModel();
            Assert.AreEqual(1, model.Level);
            Assert.AreEqual(0f, model.CurrentXp, 0.001f);
        }

        [Test]
        public void AddXp_ExactThreshold_LevelsUpWithZeroRemainder()
        {
            var model = new ExperienceModel();
            // Lv1 の閾値 = 80 + 40*(1-1) = 80
            model.AddXp(80f);
            Assert.AreEqual(2, model.Level);
            Assert.AreEqual(0f, model.CurrentXp, 0.001f);
        }

        [Test]
        public void AddXp_WithSurplus_CarriesOverToNextLevel()
        {
            var model = new ExperienceModel();
            // 80 + 10 => Lv2 余剰10
            model.AddXp(90f);
            Assert.AreEqual(2, model.Level);
            Assert.AreEqual(10f, model.CurrentXp, 0.001f);
        }

        [Test]
        public void AddXp_LargeAmount_CausesMultipleLevelUps()
        {
            var model = new ExperienceModel();
            // Lv1→2: 80, Lv2→3: 120, 合計200で Lv3 余剰0
            model.AddXp(200f);
            Assert.AreEqual(3, model.Level);
            Assert.AreEqual(0f, model.CurrentXp, 0.001f);
        }

        [Test]
        public void AddXp_AtMaxLevel_DoesNotExceedMaxAndDiscardsXp()
        {
            var model = new ExperienceModel();
            // 一括大量XPでLv10まで到達させる
            model.AddXp(999999f);
            Assert.AreEqual(ExperienceModel.MaxLevel, model.Level);
            Assert.AreEqual(0f, model.CurrentXp, 0.001f);
        }

        [Test]
        public void AddXp_AfterMaxLevel_DoesNothing()
        {
            var model = new ExperienceModel();
            model.AddXp(999999f); // Lv10 へ
            model.AddXp(100f);    // 追加は無効
            Assert.AreEqual(ExperienceModel.MaxLevel, model.Level);
            Assert.AreEqual(0f, model.CurrentXp, 0.001f);
        }

        [Test]
        public void LevelChanged_FiresCorrectNumberOfTimes()
        {
            var model = new ExperienceModel();
            int fireCount = 0;
            model.LevelChanged += _ => fireCount++;

            // Lv1→2→3 の2回レベルアップを起こす
            model.AddXp(200f); // Lv1閾値80 + Lv2閾値120 = 200
            Assert.AreEqual(2, fireCount);
        }

        [Test]
        public void LevelChanged_ReportsNewLevel()
        {
            var model = new ExperienceModel();
            int reportedLevel = 0;
            model.LevelChanged += lvl => reportedLevel = lvl;

            model.AddXp(80f); // Lv1→2
            Assert.AreEqual(2, reportedLevel);
        }
    }
}
