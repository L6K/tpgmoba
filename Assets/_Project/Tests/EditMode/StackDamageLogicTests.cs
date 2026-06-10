using NUnit.Framework;
using Enigma.Objective;

namespace Enigma.Tests.EditMode
{
    public sealed class StackDamageLogicTests
    {
        [Test]
        public void DamagePerTarget_DividesEvenlyAmongTargets()
        {
            float result = StackDamageLogic.DamagePerTarget(120f, 4);
            Assert.AreEqual(30f, result, 0.001f);
        }

        [Test]
        public void DamagePerTarget_SingleTarget_ReturnsTotalDamage()
        {
            float result = StackDamageLogic.DamagePerTarget(120f, 1);
            Assert.AreEqual(120f, result, 0.001f);
        }

        [Test]
        public void DamagePerTarget_ZeroTargets_ReturnsTotalDamage()
        {
            // count <= 0 のとき totalDamage をそのまま返す（防衛的コーディング）
            float result = StackDamageLogic.DamagePerTarget(120f, 0);
            Assert.AreEqual(120f, result, 0.001f);
        }
    }
}
