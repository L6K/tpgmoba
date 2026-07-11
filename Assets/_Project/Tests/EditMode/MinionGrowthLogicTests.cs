using NUnit.Framework;
using Enigma.Minion;

namespace Enigma.Tests
{
    public sealed class MinionGrowthLogicTests
    {
        [Test]
        public void WithinGracePeriod_IsBaseMultiplier()
        {
            // 300 秒以下は等倍
            Assert.AreEqual(1f, MinionGrowthLogic.Multiplier(0f), 1e-4f);
            Assert.AreEqual(1f, MinionGrowthLogic.Multiplier(120f), 1e-4f);
            Assert.AreEqual(1f, MinionGrowthLogic.Multiplier(300f), 1e-4f);
        }

        [Test]
        public void AfterGrace_AddsEightPercentPerMinute()
        {
            // 6 分(=猶予+1分=360s) で +8%、8 分(=猶予+3分=480s) で +24%、600s(猶予+5分)で+40%
            Assert.AreEqual(1.08f, MinionGrowthLogic.Multiplier(360f), 1e-4f);
            Assert.AreEqual(1.24f, MinionGrowthLogic.Multiplier(480f), 1e-4f);
            Assert.AreEqual(1.4f, MinionGrowthLogic.Multiplier(600f), 1e-4f);
        }

        [Test]
        public void FarLate_ClampsAtMax()
        {
            // 上限 +80%(=1.8) を超えない。900s で素の値は 1+0.08*10=1.8、
            // 1200s でクランプ済み、それ以降も 1.8 のまま
            Assert.AreEqual(1.8f, MinionGrowthLogic.Multiplier(900f), 1e-4f);
            Assert.AreEqual(1.8f, MinionGrowthLogic.Multiplier(1200f), 1e-4f);
            Assert.AreEqual(1.8f, MinionGrowthLogic.Multiplier(99999f), 1e-4f);
        }

        [Test]
        public void NegativeElapsed_IsSafeBaseMultiplier()
        {
            // 負値でも例外なく等倍
            Assert.AreEqual(1f, MinionGrowthLogic.Multiplier(-50f), 1e-4f);
        }
    }
}
