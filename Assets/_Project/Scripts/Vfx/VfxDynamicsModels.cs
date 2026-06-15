using System;

namespace Enigma.Vfx
{
    public sealed class VfxEscalationModel
    {
        private readonly int _maxTier;
        private readonly float _comboWindowSeconds;
        private readonly int _hitsPerTier;
        private float _lastHitTime = float.MinValue;

        public VfxEscalationModel(int maxTier = 3, float comboWindowSeconds = 2.0f, int hitsPerTier = 2)
        {
            _maxTier = maxTier < 1 ? 1 : maxTier;
            _comboWindowSeconds = comboWindowSeconds < 0f ? 0f : comboWindowSeconds;
            _hitsPerTier = hitsPerTier < 1 ? 1 : hitsPerTier;
        }

        public int ComboCount { get; private set; }

        public int CurrentTier { get; private set; }

        public int RegisterHit(float now)
        {
            if (ComboCount > 0 && now - _lastHitTime <= _comboWindowSeconds)
            {
                ComboCount++;
            }
            else
            {
                ComboCount = 1;
            }

            _lastHitTime = now;
            CurrentTier = ClampInt((ComboCount - 1) / _hitsPerTier, 0, _maxTier);
            return CurrentTier;
        }

        public float Multiplier(int tier)
        {
            int clampedTier = ClampInt(tier, 0, _maxTier);
            return 1f + 0.25f * clampedTier;
        }

        public void Reset()
        {
            ComboCount = 0;
            CurrentTier = 0;
            _lastHitTime = float.MinValue;
        }

        private static int ClampInt(int value, int min, int max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }
    }

    public static class HitStopModel
    {
        public static float FramesAt60(float damage, float maxHp, bool isCrit)
        {
            float ratio = maxHp <= 0f ? 1f : Clamp01(damage / maxHp);
            float frames = 2f + 12f * ratio;
            if (isCrit) frames *= 1.5f;
            return Clamp(frames, 0f, 8f);
        }

        public static float Seconds(float damage, float maxHp, bool isCrit)
        {
            return FramesAt60(damage, maxHp, isCrit) / 60f;
        }

        private static float Clamp(float value, float min, float max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        private static float Clamp01(float value)
        {
            return Clamp(value, 0f, 1f);
        }
    }

    public sealed class ScreenShakeTraumaModel
    {
        private readonly float _maxAmplitude;
        private readonly float _decayPerSecond;

        public ScreenShakeTraumaModel(float maxAmplitude, float decayPerSecond)
        {
            _maxAmplitude = maxAmplitude < 0f ? 0f : maxAmplitude;
            _decayPerSecond = decayPerSecond < 0f ? 0f : decayPerSecond;
        }

        public float Trauma { get; private set; }

        public float Amplitude => _maxAmplitude * Trauma * Trauma;

        public void AddTrauma(float amount)
        {
            if (amount <= 0f) return;
            Trauma = Clamp01(Trauma + amount);
        }

        public void Tick(float dt)
        {
            if (dt <= 0f) return;
            Trauma = Math.Max(0f, Trauma - _decayPerSecond * dt);
        }

        private static float Clamp01(float value)
        {
            if (value < 0f) return 0f;
            if (value > 1f) return 1f;
            return value;
        }
    }

    public static class BeamEnvelope
    {
        public static float WidthAt(float t, float widthStart, float widthEnd)
        {
            float s = SmoothStep01(t);
            return widthStart + (widthEnd - widthStart) * s;
        }

        public static float AlphaAt(float t)
        {
            float s = Clamp01(t);
            if (s < 0.1f) return s / 0.1f;
            if (s < 0.7f) return 1f;
            return 1f - (s - 0.7f) / 0.3f;
        }

        private static float SmoothStep01(float value)
        {
            float s = Clamp01(value);
            return s * s * (3f - 2f * s);
        }

        private static float Clamp01(float value)
        {
            if (value < 0f) return 0f;
            if (value > 1f) return 1f;
            return value;
        }
    }
}
