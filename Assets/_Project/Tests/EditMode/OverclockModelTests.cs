using Enigma.Ability;
using NUnit.Framework;

namespace Enigma.Tests.EditMode
{
    public sealed class OverclockModelTests
    {
        private const float Tolerance = 1e-4f;

        [Test]
        public void Evaluate_ZeroCharge_HasNoCostAndCanCast()
        {
            var model = new OverclockModel(maxChargeSeconds: 1f, maxAmp: 2f, maxCostFraction: 0.25f);

            OverclockResult result = model.Evaluate(0f, currentHp: 0f, maxHp: 100f, currentShield: 0f);

            Assert.AreEqual(0f, result.Charge01, Tolerance);
            Assert.AreEqual(1f, result.AmpFactor, Tolerance);
            Assert.AreEqual(0f, result.HpCost, Tolerance);
            Assert.AreEqual(0f, result.ShieldCost, Tolerance);
            Assert.IsTrue(result.CanCast);
        }

        [Test]
        public void Evaluate_MaxCharge_UsesMaxAmpAndMaxCost()
        {
            var model = new OverclockModel(maxChargeSeconds: 2f, maxAmp: 1.8f, maxCostFraction: 0.25f);

            OverclockResult result = model.Evaluate(2f, currentHp: 100f, maxHp: 200f, currentShield: 0f);

            Assert.AreEqual(1f, result.Charge01, Tolerance);
            Assert.AreEqual(1.8f, result.AmpFactor, Tolerance);
            Assert.AreEqual(50f, result.HpCost, Tolerance);
            Assert.AreEqual(0f, result.ShieldCost, Tolerance);
            Assert.IsTrue(result.CanCast);
        }

        [Test]
        public void Evaluate_ShieldPaysCostFirst()
        {
            var model = new OverclockModel(maxChargeSeconds: 1f, maxAmp: 2f, maxCostFraction: 0.25f);

            OverclockResult result = model.Evaluate(1f, currentHp: 5f, maxHp: 100f, currentShield: 30f);

            Assert.AreEqual(0f, result.HpCost, Tolerance);
            Assert.AreEqual(25f, result.ShieldCost, Tolerance);
            Assert.IsTrue(result.CanCast);
        }

        [Test]
        public void Evaluate_InsufficientShieldAndHp_CannotCast()
        {
            var model = new OverclockModel(maxChargeSeconds: 1f, maxAmp: 2f, maxCostFraction: 0.25f, minHpAfter: 1f);

            OverclockResult result = model.Evaluate(1f, currentHp: 10f, maxHp: 100f, currentShield: 5f);

            Assert.AreEqual(20f, result.HpCost, Tolerance);
            Assert.AreEqual(5f, result.ShieldCost, Tolerance);
            Assert.IsFalse(result.CanCast);
        }

        [Test]
        public void Evaluate_ClampsChargeAboveMax()
        {
            var model = new OverclockModel(maxChargeSeconds: 1f, maxAmp: 1.5f, maxCostFraction: 0.1f);

            OverclockResult result = model.Evaluate(5f, currentHp: 100f, maxHp: 100f, currentShield: 0f);

            Assert.AreEqual(1f, result.Charge01, Tolerance);
            Assert.AreEqual(1.5f, result.AmpFactor, Tolerance);
            Assert.AreEqual(10f, result.HpCost, Tolerance);
        }

        [Test]
        public void AmpAt_IsLinearAndClamped()
        {
            var model = new OverclockModel(maxAmp: 1.8f);

            Assert.AreEqual(1f, model.AmpAt(0f), Tolerance);
            Assert.AreEqual(1.4f, model.AmpAt(0.5f), Tolerance);
            Assert.AreEqual(1.8f, model.AmpAt(1f), Tolerance);
            Assert.AreEqual(1f, model.AmpAt(-1f), Tolerance);
            Assert.AreEqual(1.8f, model.AmpAt(2f), Tolerance);
        }
    }
}
