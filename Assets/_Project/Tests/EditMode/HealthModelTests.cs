using NUnit.Framework;
using Enigma.Combat;

namespace Enigma.Tests
{
    public sealed class HealthModelTests
    {
        [Test]
        public void TakeDamage_ReducesCurrentHp()
        {
            var model = new HealthModel(100f);
            model.TakeDamage(30f);
            Assert.AreEqual(70f, model.CurrentHp, 0.001f);
        }

        [Test]
        public void TakeDamage_ClampsToZero()
        {
            var model = new HealthModel(100f);
            model.TakeDamage(200f);
            Assert.AreEqual(0f, model.CurrentHp, 0.001f);
        }

        [Test]
        public void Died_FiresOnlyOnce()
        {
            var model = new HealthModel(100f);
            int count = 0;
            model.Died += () => count++;

            model.TakeDamage(100f); // 死亡
            model.TakeDamage(1f);   // 死亡中は無視 → Died は再発火しない

            Assert.AreEqual(1, count);
        }

        [Test]
        public void TakeDamage_WhileDead_IsIgnored()
        {
            var model = new HealthModel(100f);
            model.TakeDamage(100f); // 死亡
            model.TakeDamage(50f);  // 無視されるはず

            Assert.AreEqual(0f, model.CurrentHp, 0.001f);
            Assert.IsTrue(model.IsDead);
        }

        [Test]
        public void Revive_RestoresFullHpAndClearsDeadState()
        {
            var model = new HealthModel(100f);
            model.TakeDamage(100f);
            model.Revive();

            Assert.AreEqual(100f, model.CurrentHp, 0.001f);
            Assert.IsFalse(model.IsDead);
        }

        [Test]
        public void Changed_FiresOnTakeDamage()
        {
            var model = new HealthModel(100f);
            float firedCurrent = -1f;
            float firedMax = -1f;
            model.Changed += (cur, max) => { firedCurrent = cur; firedMax = max; };

            model.TakeDamage(40f);

            Assert.AreEqual(60f, firedCurrent, 0.001f);
            Assert.AreEqual(100f, firedMax, 0.001f);
        }

        [Test]
        public void Changed_FiresOnRevive()
        {
            var model = new HealthModel(100f);
            model.TakeDamage(100f);

            float firedCurrent = -1f;
            model.Changed += (cur, max) => firedCurrent = cur;
            model.Revive();

            Assert.AreEqual(100f, firedCurrent, 0.001f);
        }
    }
}
