using NUnit.Framework;
using Enigma.GameModes;

namespace Enigma.Tests
{
    public sealed class RespawnTimerLogicTests
    {
        [Test]
        public void WithinGracePeriod_IsBaseDelay()
        {
            // 300 秒以下は基本遅延(5秒)のまま
            Assert.AreEqual(5f, RespawnTimerLogic.Delay(0f), 1e-4f);
            Assert.AreEqual(5f, RespawnTimerLogic.Delay(120f), 1e-4f);
            Assert.AreEqual(5f, RespawnTimerLogic.Delay(300f), 1e-4f);
        }

        [Test]
        public void AfterGrace_AddsOnePointFiveSecondsPerMinute()
        {
            // 600s(猶予+5分) で 5 + 1.5*5 = 12.5 秒
            Assert.AreEqual(12.5f, RespawnTimerLogic.Delay(600f), 1e-4f);
        }

        [Test]
        public void FarLate_ClampsAtMax()
        {
            // 上限 20 秒を超えない。900s で素の値は 5+1.5*10=20 秒（ちょうど上限）、
            // 1200s(素の値27.5)でクランプ済み、それ以降も 20 のまま
            Assert.AreEqual(20f, RespawnTimerLogic.Delay(900f), 1e-4f);
            Assert.AreEqual(20f, RespawnTimerLogic.Delay(1200f), 1e-4f);
            Assert.AreEqual(20f, RespawnTimerLogic.Delay(99999f), 1e-4f);
        }

        [Test]
        public void NegativeElapsed_IsSafeBaseDelay()
        {
            // 負値でも例外なく基本遅延
            Assert.AreEqual(5f, RespawnTimerLogic.Delay(-50f), 1e-4f);
        }
    }
}
