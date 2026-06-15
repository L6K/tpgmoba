using Enigma.Vfx;
using NUnit.Framework;

namespace Enigma.Tests
{
    public sealed class VfxDynamicsModelsTests
    {
        private const float Tolerance = 0.0001f;

        [Test]
        public void RegisterHit_FirstHit_SetsComboCountAndTierZero()
        {
            var model = new VfxEscalationModel();

            int tier = model.RegisterHit(10f);

            Assert.AreEqual(1, model.ComboCount);
            Assert.AreEqual(0, model.CurrentTier);
            Assert.AreEqual(0, tier);
        }

        [Test]
        public void RegisterHit_WithinWindow_IncrementsComboAndTierPerHits()
        {
            var model = new VfxEscalationModel(maxTier: 3, comboWindowSeconds: 1f, hitsPerTier: 2);

            model.RegisterHit(0f);
            model.RegisterHit(0.5f);
            int tier = model.RegisterHit(1.0f);

            Assert.AreEqual(3, model.ComboCount);
            Assert.AreEqual(1, model.CurrentTier);
            Assert.AreEqual(1, tier);
        }

        [Test]
        public void RegisterHit_AfterWindow_ResetsComboCount()
        {
            var model = new VfxEscalationModel(comboWindowSeconds: 1f);

            model.RegisterHit(0f);
            model.RegisterHit(0.5f);
            model.RegisterHit(2f);

            Assert.AreEqual(1, model.ComboCount);
            Assert.AreEqual(0, model.CurrentTier);
        }

        [Test]
        public void RegisterHit_TierCapsAtMaxTier()
        {
            var model = new VfxEscalationModel(maxTier: 2, comboWindowSeconds: 1f, hitsPerTier: 1);

            for (int i = 0; i < 8; i++)
            {
                model.RegisterHit(i * 0.1f);
            }

            Assert.AreEqual(2, model.CurrentTier);
        }

        [Test]
        public void Multiplier_ReturnsQuarterStepScale()
        {
            var model = new VfxEscalationModel();

            Assert.AreEqual(1f, model.Multiplier(0), Tolerance);
            Assert.AreEqual(1.5f, model.Multiplier(2), Tolerance);
        }

        [Test]
        public void Constructor_ClampsInvalidSettings()
        {
            var model = new VfxEscalationModel(maxTier: 0, comboWindowSeconds: -1f, hitsPerTier: 0);

            model.RegisterHit(0f);
            model.RegisterHit(0f);
            model.RegisterHit(0f);

            Assert.AreEqual(3, model.ComboCount);
            Assert.AreEqual(1, model.CurrentTier);
            Assert.AreEqual(1.25f, model.Multiplier(5), Tolerance);
        }

        [Test]
        public void Reset_ClearsComboAndTier()
        {
            var model = new VfxEscalationModel();
            model.RegisterHit(0f);
            model.RegisterHit(0.5f);

            model.Reset();

            Assert.AreEqual(0, model.ComboCount);
            Assert.AreEqual(0, model.CurrentTier);
        }

        [Test]
        public void HitStop_FramesAt60_UsesDamageRatioAndClamp()
        {
            Assert.AreEqual(2f, HitStopModel.FramesAt60(0f, 100f, false), Tolerance);
            Assert.AreEqual(8f, HitStopModel.FramesAt60(100f, 100f, false), Tolerance);
            Assert.AreEqual(8f, HitStopModel.FramesAt60(100f, 0f, false), Tolerance);
        }

        [Test]
        public void HitStop_CritProducesLargerValueWithinCap()
        {
            float normal = HitStopModel.FramesAt60(20f, 100f, false);
            float crit = HitStopModel.FramesAt60(20f, 100f, true);

            Assert.Greater(crit, normal);
            Assert.LessOrEqual(crit, 8f);
        }

        [Test]
        public void HitStop_Seconds_ReturnsFramesDividedBySixty()
        {
            float frames = HitStopModel.FramesAt60(20f, 100f, true);

            Assert.AreEqual(frames / 60f, HitStopModel.Seconds(20f, 100f, true), Tolerance);
        }

        [Test]
        public void ScreenShake_AddTraumaAndTick_UpdateAmplitude()
        {
            var model = new ScreenShakeTraumaModel(maxAmplitude: 10f, decayPerSecond: 0.25f);

            model.AddTrauma(0.5f);

            Assert.AreEqual(0.5f, model.Trauma, Tolerance);
            Assert.AreEqual(2.5f, model.Amplitude, Tolerance);

            model.Tick(1f);

            Assert.AreEqual(0.25f, model.Trauma, Tolerance);
            Assert.AreEqual(0.625f, model.Amplitude, Tolerance);
        }

        [Test]
        public void ScreenShake_TraumaDoesNotUnderflowOrOverflow()
        {
            var model = new ScreenShakeTraumaModel(maxAmplitude: 10f, decayPerSecond: 1f);

            model.AddTrauma(2f);
            model.Tick(2f);

            Assert.AreEqual(0f, model.Trauma, Tolerance);
            Assert.AreEqual(0f, model.Amplitude, Tolerance);
        }

        [Test]
        public void ScreenShake_NegativeInputsDoNotChangeTrauma()
        {
            var model = new ScreenShakeTraumaModel(maxAmplitude: 10f, decayPerSecond: 1f);
            model.AddTrauma(0.5f);

            model.AddTrauma(-1f);
            model.Tick(-1f);

            Assert.AreEqual(0.5f, model.Trauma, Tolerance);
        }

        [Test]
        public void ScreenShake_ConstructorClampsNegativeSettings()
        {
            var model = new ScreenShakeTraumaModel(maxAmplitude: -10f, decayPerSecond: -1f);

            model.AddTrauma(0.5f);
            model.Tick(1f);

            Assert.AreEqual(0.5f, model.Trauma, Tolerance);
            Assert.AreEqual(0f, model.Amplitude, Tolerance);
        }

        [Test]
        public void BeamEnvelope_WidthAt_ReturnsEndpoints()
        {
            Assert.AreEqual(0.25f, BeamEnvelope.WidthAt(0f, 0.25f, 0.75f), Tolerance);
            Assert.AreEqual(0.75f, BeamEnvelope.WidthAt(1f, 0.25f, 0.75f), Tolerance);
        }

        [Test]
        public void BeamEnvelope_WidthAt_IsMonotonicWhenEndIsGreater()
        {
            float previous = BeamEnvelope.WidthAt(0f, 0.25f, 0.75f);
            for (int i = 1; i <= 10; i++)
            {
                float current = BeamEnvelope.WidthAt(i / 10f, 0.25f, 0.75f);
                Assert.GreaterOrEqual(current, previous);
                previous = current;
            }
        }

        [Test]
        public void BeamEnvelope_AlphaAt_FadesInHoldsAndFadesOut()
        {
            Assert.AreEqual(0f, BeamEnvelope.AlphaAt(0f), Tolerance);
            Assert.AreEqual(0.5f, BeamEnvelope.AlphaAt(0.05f), Tolerance);
            Assert.AreEqual(1f, BeamEnvelope.AlphaAt(0.4f), Tolerance);
            Assert.AreEqual(0f, BeamEnvelope.AlphaAt(1f), Tolerance);
        }

        [Test]
        public void BeamEnvelope_AlphaAt_ClampsOutOfRangeTimes()
        {
            Assert.AreEqual(0f, BeamEnvelope.AlphaAt(-1f), Tolerance);
            Assert.AreEqual(0f, BeamEnvelope.AlphaAt(2f), Tolerance);
        }
    }
}
