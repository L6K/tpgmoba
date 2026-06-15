using NUnit.Framework;
using Enigma.Minion;

namespace Enigma.Tests
{
    public sealed class MinionScalingTests
    {
        [Test]
        public void WithinGracePeriod_IsBaseMultiplier()
        {
            // 3 分以下は等倍
            Assert.AreEqual(1f, MinionScaling.MultiplierAt(0f), 1e-4f);
            Assert.AreEqual(1f, MinionScaling.MultiplierAt(120f), 1e-4f);
            Assert.AreEqual(1f, MinionScaling.MultiplierAt(180f), 1e-4f);
        }

        [Test]
        public void AfterGrace_AddsEightPercentPerMinute()
        {
            // 4 分(=猶予+1分) で +8%、6 分(=猶予+3分) で +24%
            Assert.AreEqual(1.08f, MinionScaling.MultiplierAt(240f), 1e-4f);
            Assert.AreEqual(1.24f, MinionScaling.MultiplierAt(360f), 1e-4f);
        }

        [Test]
        public void FarLate_ClampsAtMax()
        {
            // 上限 +200%(=3.0) を超えない。猶予+25分(=1500秒)で素の値は 1+2.0=3.0、
            // それ以降も 3.0 でクランプ
            Assert.AreEqual(3.0f, MinionScaling.MultiplierAt(180f + 25f * 60f), 1e-4f);
            Assert.AreEqual(3.0f, MinionScaling.MultiplierAt(99999f), 1e-4f);
        }

        [Test]
        public void NegativeElapsed_IsSafeBaseMultiplier()
        {
            // 負値でも例外なく等倍
            Assert.AreEqual(1f, MinionScaling.MultiplierAt(-50f), 1e-4f);
        }
    }
}
