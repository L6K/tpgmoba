using Enigma.Vfx;
using NUnit.Framework;

namespace Enigma.Tests
{
    public sealed class VfxDynamicsModelsTests
    {
        private const float Tolerance = 1e-4f;

        [Test]
        public void VfxEscalation_FirstHit_StartsComboAtTierZero()
        {
            var model = new VfxEscalationModel();

            int tier = model.RegisterHit(10f);

            Assert.AreEqual(1, model.ComboCount);
            Assert.AreEqual(0, tier);
            Assert.AreEqual(0, model.CurrentTier);
        }

        [Test]
        public void VfxEscalation_WindowHits_IncreaseComboAndTierByHitsPerTier()
        {
            var model = new VfxEscalationModel(maxTier: 3, comboWindowSeconds: 2f, hitsPerTier: 2);

            model.RegisterHit(0f);
            model.RegisterHit(1f);
            int tier = model.RegisterHit(2f);

            Assert.AreEqual(3, model.ComboCount);
            Assert.AreEqual(1, tier);
        }

        [Test]
        public void VfxEscalation_WindowExceeded_ResetsCombo()
        {
            var model = new VfxEscalationModel(maxTier: 3, comboWindowSeconds: 1f, hitsPerTier: 1);

            model.RegisterHit(0f);
            model.RegisterHit(0.5f);
            int tier = model.RegisterHit(2f);

            Assert.AreEqual(1, model.ComboCount);
            Assert.AreEqual(0, tier);
        }

        [Test]
        public void VfxEscalation_TierCapsAtMaxTier()
        {
            var model = new VfxEscalationModel(maxTier: 2, comboWindowSeconds: 10f, hitsPerTier: 1);

            int tier = 0;
            for (int i = 0; i < 10; i++)
                tier = model.RegisterHit(i);

            Assert.AreEqual(2, tier);
            Assert.AreEqual(2, model.CurrentTier);
        }

        [Test]
        public void VfxEscalation_Multiplier_UsesClampedTier()
        {
            var model = new VfxEscalationModel(maxTier: 3);

            Assert.AreEqual(1f, model.Multiplier(0), Tolerance);
            Assert.AreEqual(1.5f, model.Multiplier(2), Tolerance);
            Assert.AreEqual(1f, model.Multiplier(-5), Tolerance);
            Assert.AreEqual(1.75f, model.Multiplier(99), Tolerance);
        }

        [Test]
        public void VfxEscalation_CtorClampsInvalidArguments()
        {
            var model = new VfxEscalationModel(maxTier: 0, comboWindowSeconds: -1f, hitsPerTier: 0);

            // window は 0 にクランプされるため、コンボ継続は「同時刻(差<=0)ヒット」でのみ成立する。
            // hitsPerTier=0→1・maxTier=0→1 のクランプが効いていれば 2 連で ComboCount=2 / tier=1。
            model.RegisterHit(1f);
            int tier = model.RegisterHit(1f);

            Assert.AreEqual(2, model.ComboCount);
            Assert.AreEqual(1, tier);
        }

        [Test]
        public void VfxEscalation_Reset_ClearsState()
        {
            var model = new VfxEscalationModel();
            model.RegisterHit(0f);
            model.RegisterHit(1f);

            model.Reset();

            Assert.AreEqual(0, model.ComboCount);
            Assert.AreEqual(0, model.CurrentTier);
            Assert.AreEqual(0, model.RegisterHit(100f));
            Assert.AreEqual(1, model.ComboCount);
        }

        [Test]
        public void HitStop_FramesAt60_UsesDamageRatioAndClamp()
        {
            Assert.AreEqual(2f, HitStopModel.FramesAt60(0f, 100f, false), Tolerance);
            Assert.AreEqual(8f, HitStopModel.FramesAt60(100f, 100f, false), Tolerance);
        }

        [Test]
        public void HitStop_CritIncreasesFramesButRespectsCap()
        {
            float normal = HitStopModel.FramesAt60(20f, 100f, false);
            float crit = HitStopModel.FramesAt60(20f, 100f, true);

            Assert.Greater(crit, normal);
            Assert.AreEqual(8f, HitStopModel.FramesAt60(100f, 100f, true), Tolerance);
        }

        [Test]
        public void HitStop_Seconds_ReturnsFramesDividedBySixty()
        {
            float frames = HitStopModel.FramesAt60(25f, 100f, false);

            Assert.AreEqual(frames / 60f, HitStopModel.Seconds(25f, 100f, false), Tolerance);
        }

        [Test]
        public void HitStop_MaxHpNonPositive_TreatsRatioAsZero()
        {
            Assert.AreEqual(2f, HitStopModel.FramesAt60(100f, 0f, false), Tolerance);
        }

        [Test]
        public void ScreenShake_AddTraumaAndTick_UpdateAmplitude()
        {
            var model = new ScreenShakeTraumaModel(maxAmplitude: 10f, decayPerSecond: 0.5f);

            model.AddTrauma(0.5f);
            Assert.AreEqual(0.5f, model.Trauma, Tolerance);
            Assert.AreEqual(2.5f, model.Amplitude, Tolerance);

            model.Tick(0.5f);
            Assert.AreEqual(0.25f, model.Trauma, Tolerance);
            Assert.AreEqual(0.625f, model.Amplitude, Tolerance);

            model.Tick(10f);
            Assert.AreEqual(0f, model.Trauma, Tolerance);
        }

        [Test]
        public void ScreenShake_ClampsTraumaAndIgnoresNegativeInput()
        {
            var model = new ScreenShakeTraumaModel(maxAmplitude: 1f, decayPerSecond: 1f);

            model.AddTrauma(2f);
            Assert.AreEqual(1f, model.Trauma, Tolerance);
            model.AddTrauma(-1f);
            Assert.AreEqual(1f, model.Trauma, Tolerance);
            model.Tick(-1f);
            Assert.AreEqual(1f, model.Trauma, Tolerance);
        }

        [Test]
        public void ScreenShake_CtorClampsNegativeArguments()
        {
            var model = new ScreenShakeTraumaModel(maxAmplitude: -1f, decayPerSecond: -1f);

            model.AddTrauma(1f);
            model.Tick(1f);

            Assert.AreEqual(1f, model.Trauma, Tolerance);
            Assert.AreEqual(0f, model.Amplitude, Tolerance);
        }

        [Test]
        public void BeamEnvelope_WidthAt_UsesSmoothstepAndClampsT()
        {
            Assert.AreEqual(0.2f, BeamEnvelope.WidthAt(0f, 0.2f, 1f), Tolerance);
            Assert.AreEqual(1f, BeamEnvelope.WidthAt(1f, 0.2f, 1f), Tolerance);
            Assert.AreEqual(0.2f, BeamEnvelope.WidthAt(-1f, 0.2f, 1f), Tolerance);
            Assert.AreEqual(1f, BeamEnvelope.WidthAt(2f, 0.2f, 1f), Tolerance);
        }

        [Test]
        public void BeamEnvelope_WidthAt_IsMonotonicWhenEndIsGreater()
        {
            float a = BeamEnvelope.WidthAt(0.25f, 0.2f, 1f);
            float b = BeamEnvelope.WidthAt(0.5f, 0.2f, 1f);
            float c = BeamEnvelope.WidthAt(0.75f, 0.2f, 1f);

            Assert.Greater(b, a);
            Assert.Greater(c, b);
        }

        [Test]
        public void BeamEnvelope_AlphaAt_FollowsAttackHoldFadeShape()
        {
            Assert.AreEqual(0f, BeamEnvelope.AlphaAt(0f), Tolerance);
            Assert.AreEqual(0.5f, BeamEnvelope.AlphaAt(0.05f), Tolerance);
            Assert.AreEqual(1f, BeamEnvelope.AlphaAt(0.4f), Tolerance);
            Assert.AreEqual(1f, BeamEnvelope.AlphaAt(0.7f), Tolerance);
            Assert.AreEqual(0f, BeamEnvelope.AlphaAt(1f), Tolerance);
        }
    }
}
