using NUnit.Framework;
using Enigma.Combat;

namespace Enigma.Tests.EditMode
{
    public sealed class OvertimeDecayLogicTests
    {
        [Test]
        public void オーバータイム前は減衰しない()
        {
            Assert.AreEqual(0f, OvertimeDecayLogic.DamagePerSecond(800f, 899f));
        }

        [Test]
        public void オーバータイム後は最大HPの1パーセントを毎秒減衰()
        {
            Assert.AreEqual(8f, OvertimeDecayLogic.DamagePerSecond(800f, 900f), 0.001f);
            Assert.AreEqual(25f, OvertimeDecayLogic.DamagePerSecond(2500f, 1200f), 0.001f);
        }

        [Test]
        public void 開始時刻はカスタム可能()
        {
            Assert.AreEqual(0f, OvertimeDecayLogic.DamagePerSecond(800f, 100f, 200f));
            Assert.AreEqual(8f, OvertimeDecayLogic.DamagePerSecond(800f, 200f, 200f), 0.001f);
        }

        [Test]
        public void 不正な最大HPは0を返す()
        {
            Assert.AreEqual(0f, OvertimeDecayLogic.DamagePerSecond(0f, 9999f));
            Assert.AreEqual(0f, OvertimeDecayLogic.DamagePerSecond(-10f, 9999f));
        }
    }
}
