using NUnit.Framework;
using Enigma.Combat;

namespace Enigma.Tests
{
    public sealed class HealthModelTests
    {
        private const float Tolerance = 1e-4f;

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
        public void Heal_ClampsToMaxHp()
        {
            var model = new HealthModel(100f);
            model.TakeDamage(30f);
            model.Heal(50f);
            Assert.AreEqual(100f, model.CurrentHp, 0.001f);
        }

        [Test]
        public void Heal_DoesNothingWhenDead()
        {
            var model = new HealthModel(100f);
            model.TakeDamage(100f);
            model.Heal(50f);
            Assert.IsTrue(model.IsDead);
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

        [Test]
        public void Revived_FiresOnRevive()
        {
            var model = new HealthModel(100f);
            model.TakeDamage(100f);

            int count = 0;
            model.Revived += () => count++;
            model.Revive();

            Assert.AreEqual(1, count);
        }

        [Test]
        public void Revived_FiresEvenWhenNotDead()
        {
            var model = new HealthModel(100f);

            int count = 0;
            model.Revived += () => count++;
            model.Revive(); // 未死亡からの Revive でも発火してよい

            Assert.AreEqual(1, count);
        }

        [Test]
        public void AddShield_IncreasesShieldTotal()
        {
            var model = new HealthModel(100f);

            model.AddShield(25f, 2f);

            Assert.AreEqual(25f, model.Shield, Tolerance);
        }

        [Test]
        public void TakeDamage_LessThanShield_ReducesShieldOnly()
        {
            var model = new HealthModel(100f);
            int changedCount = 0;
            model.Changed += (_, _) => changedCount++;

            model.AddShield(30f, 2f);
            model.TakeDamage(10f);

            Assert.AreEqual(20f, model.Shield, Tolerance);
            Assert.AreEqual(100f, model.CurrentHp, Tolerance);
            Assert.AreEqual(0, changedCount);
        }

        [Test]
        public void TakeDamage_ExceedingShield_DamagesRemainingHp()
        {
            var model = new HealthModel(100f);

            model.AddShield(30f, 2f);
            model.TakeDamage(50f);

            Assert.AreEqual(0f, model.Shield, Tolerance);
            Assert.AreEqual(80f, model.CurrentHp, Tolerance);
        }

        [Test]
        public void TakeDamage_ConsumesMultipleShieldsInFifoOrder()
        {
            var model = new HealthModel(100f);

            model.AddShield(30f, 1f);
            model.AddShield(50f, 10f);
            model.TakeDamage(20f);
            model.Tick(1.1f);

            Assert.AreEqual(50f, model.Shield, Tolerance);
        }

        [Test]
        public void Tick_ExpiresShieldAndReducesTotal()
        {
            var model = new HealthModel(100f);

            model.AddShield(20f, 1f);
            model.AddShield(30f, 2f);
            model.Tick(1.1f);

            Assert.AreEqual(30f, model.Shield, Tolerance);
        }

        [Test]
        public void ShieldAbsorbsAllDamage_PreventsDeath()
        {
            var model = new HealthModel(100f);

            model.AddShield(150f, 2f);
            model.TakeDamage(120f);

            Assert.IsFalse(model.IsDead);
            Assert.AreEqual(100f, model.CurrentHp, Tolerance);
            Assert.AreEqual(30f, model.Shield, Tolerance);
        }

        [Test]
        public void AddShield_WithNonPositiveValues_IsIgnored()
        {
            var model = new HealthModel(100f);
            int shieldChangedCount = 0;
            model.ShieldChanged += _ => shieldChangedCount++;

            model.AddShield(0f, 1f);
            model.AddShield(-1f, 1f);
            model.AddShield(10f, 0f);
            model.AddShield(10f, -1f);

            Assert.AreEqual(0f, model.Shield, Tolerance);
            Assert.AreEqual(0, shieldChangedCount);
        }

        [Test]
        public void AddShield_WhileDead_IsIgnored()
        {
            var model = new HealthModel(100f);

            model.TakeDamage(100f);
            model.AddShield(50f, 2f);

            Assert.AreEqual(0f, model.Shield, Tolerance);
        }

        [Test]
        public void ShieldChanged_FiresOnAddAbsorbAndExpire()
        {
            var model = new HealthModel(100f);
            int shieldChangedCount = 0;
            model.ShieldChanged += _ => shieldChangedCount++;

            model.AddShield(30f, 1f);
            model.TakeDamage(10f);
            model.Tick(1.1f);

            Assert.AreEqual(3, shieldChangedCount);
        }

        [Test]
        public void ShieldChanged_DoesNotFireWhenShieldDoesNotChange()
        {
            var model = new HealthModel(100f);
            int shieldChangedCount = 0;
            model.ShieldChanged += _ => shieldChangedCount++;

            model.TakeDamage(10f);
            model.Tick(-1f);
            model.AddShield(0f, 1f);

            Assert.AreEqual(0, shieldChangedCount);
        }

        [Test]
        public void HealAndAddMaxHp_DoNotChangeShield()
        {
            var model = new HealthModel(100f);

            model.TakeDamage(20f);
            model.AddShield(40f, 2f);
            model.Heal(10f);
            model.AddMaxHp(25f);

            Assert.AreEqual(40f, model.Shield, Tolerance);
        }

        [Test]
        public void Revive_ClearsShield()
        {
            var model = new HealthModel(100f);
            int shieldChangedCount = 0;
            model.ShieldChanged += _ => shieldChangedCount++;

            model.AddShield(40f, 2f);
            model.Revive();

            Assert.AreEqual(0f, model.Shield, Tolerance);
            Assert.AreEqual(2, shieldChangedCount);
        }

        [Test]
        public void Tick_WithNegativeValue_IsIgnoredForShield()
        {
            var model = new HealthModel(100f);

            model.AddShield(20f, 0.5f);
            model.Tick(-1f);
            model.Tick(0.4f);

            Assert.AreEqual(20f, model.Shield, Tolerance);
        }

        [Test]
        public void TakeDamage_WithShieldAndLethalRemainder_FiresDiedOnce()
        {
            var model = new HealthModel(100f);
            int diedCount = 0;
            model.Died += () => diedCount++;

            model.AddShield(25f, 2f);
            model.TakeDamage(150f);
            model.TakeDamage(1f);

            Assert.IsTrue(model.IsDead);
            Assert.AreEqual(0f, model.CurrentHp, Tolerance);
            Assert.AreEqual(0f, model.Shield, Tolerance);
            Assert.AreEqual(1, diedCount);
        }
    }
}
