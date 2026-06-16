using Enigma.Vfx;
using NUnit.Framework;

namespace Enigma.Tests
{
    public sealed class PlayerHitFeedbackModelTests
    {
        private const float Tolerance = 1e-4f;

        [Test]
        public void Evaluate_DamageNonPositive_ReturnsEmptyFeedback()
        {
            HitFeedback feedback = PlayerHitFeedbackModel.Evaluate(0f, 100f, 10f, true, 90f);

            Assert.AreEqual(0f, feedback.FlashAlpha, Tolerance);
            Assert.AreEqual(0f, feedback.FlashSeconds, Tolerance);
            Assert.AreEqual(0f, feedback.VignetteStrength, Tolerance);
            Assert.AreEqual(0f, feedback.DirectionDegrees, Tolerance);
        }

        [Test]
        public void Evaluate_LightDamage_UsesSeverityForFlash()
        {
            HitFeedback feedback = PlayerHitFeedbackModel.Evaluate(10f, 1000f, 900f, false, 45f);

            Assert.AreEqual(0.161f, feedback.FlashAlpha, Tolerance);
            Assert.AreEqual(0.125f, feedback.FlashSeconds, Tolerance);
            Assert.AreEqual(0f, feedback.VignetteStrength, Tolerance);
            Assert.AreEqual(45f, feedback.DirectionDegrees, Tolerance);
        }

        [Test]
        public void Evaluate_HeavyDamage_ClampsFlashAlphaAndSeconds()
        {
            HitFeedback feedback = PlayerHitFeedbackModel.Evaluate(900f, 1000f, 400f, false, 0f);

            Assert.AreEqual(0.85f, feedback.FlashAlpha, Tolerance);
            Assert.AreEqual(0.5f, feedback.FlashSeconds, Tolerance);
        }

        [Test]
        public void Evaluate_CritIncreasesFlashAlphaWithinCap()
        {
            HitFeedback normal = PlayerHitFeedbackModel.Evaluate(100f, 1000f, 800f, false, 0f);
            HitFeedback crit = PlayerHitFeedbackModel.Evaluate(100f, 1000f, 800f, true, 0f);

            Assert.Greater(crit.FlashAlpha, normal.FlashAlpha);
            Assert.LessOrEqual(crit.FlashAlpha, 0.85f);
        }

        [Test]
        public void Evaluate_LowHpDrivesVignetteStrength()
        {
            Assert.AreEqual(0f, PlayerHitFeedbackModel.Evaluate(1f, 100f, 30f, false, 0f).VignetteStrength, Tolerance);
            Assert.AreEqual(0.5f, PlayerHitFeedbackModel.Evaluate(1f, 100f, 15f, false, 0f).VignetteStrength, Tolerance);
            Assert.AreEqual(1f, PlayerHitFeedbackModel.Evaluate(1f, 100f, 0f, false, 0f).VignetteStrength, Tolerance);
        }

        [Test]
        public void NormalizeAngle_WrapsToZeroInclusiveThreeSixtyExclusive()
        {
            Assert.AreEqual(270f, PlayerHitFeedbackModel.NormalizeAngle(-90f), Tolerance);
            Assert.AreEqual(90f, PlayerHitFeedbackModel.NormalizeAngle(450f), Tolerance);
            Assert.AreEqual(0f, PlayerHitFeedbackModel.NormalizeAngle(360f), Tolerance);
            Assert.AreEqual(0f, PlayerHitFeedbackModel.NormalizeAngle(0f), Tolerance);
            Assert.AreEqual(179.5f, PlayerHitFeedbackModel.NormalizeAngle(179.5f), Tolerance);
        }

        [Test]
        public void Evaluate_MaxHpNonPositive_UsesZeroSeverityAndNoVignette()
        {
            HitFeedback feedback = PlayerHitFeedbackModel.Evaluate(50f, 0f, 0f, false, -1f);

            Assert.AreEqual(0.15f, feedback.FlashAlpha, Tolerance);
            Assert.AreEqual(0.12f, feedback.FlashSeconds, Tolerance);
            Assert.AreEqual(0f, feedback.VignetteStrength, Tolerance);
            Assert.AreEqual(359f, feedback.DirectionDegrees, Tolerance);
        }
    }
}
