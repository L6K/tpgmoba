namespace Enigma.Vfx
{
    public readonly struct HitFeedback
    {
        public readonly float FlashAlpha;
        public readonly float FlashSeconds;
        public readonly float VignetteStrength;
        public readonly float DirectionDegrees;

        public HitFeedback(float flashAlpha, float flashSeconds, float vignetteStrength, float directionDegrees)
        {
            FlashAlpha = flashAlpha;
            FlashSeconds = flashSeconds;
            VignetteStrength = vignetteStrength;
            DirectionDegrees = directionDegrees;
        }
    }

    public static class PlayerHitFeedbackModel
    {
        public static HitFeedback Evaluate(float damage, float maxHp, float currentHpAfter, bool isCrit, float directionDegrees)
        {
            if (damage <= 0f)
                return default;

            float severity = maxHp <= 0f ? 0f : Clamp01(damage / maxHp);
            float flashAlpha = Clamp(0.15f + 1.1f * severity, 0f, 0.85f);
            if (isCrit)
                flashAlpha = Clamp(flashAlpha * 1.25f, 0f, 0.85f);

            float flashSeconds = Clamp(0.12f + 0.5f * severity, 0.12f, 0.5f);

            return new HitFeedback(
                flashAlpha,
                flashSeconds,
                LowHpVignette(currentHpAfter, maxHp),
                NormalizeAngle(directionDegrees));
        }

        /// <summary>
        /// 残HP割合から低HPビネット強度(0..1)を返す純関数。被ダメと独立に、現在HPから都度算出して
        /// 回復/リスポーンでビネットが正しく消えるようにする（被弾時だけでなく毎フレーム駆動できる）。
        /// hpFrac>=0.30 で 0、0 で 1、その間は線形。
        /// </summary>
        public static float LowHpVignette(float currentHp, float maxHp)
        {
            float hpFrac = maxHp <= 0f ? 1f : Clamp01(currentHp / maxHp);
            return hpFrac >= 0.30f ? 0f : Clamp01((0.30f - hpFrac) / 0.30f);
        }

        public static float NormalizeAngle(float degrees)
        {
            float normalized = degrees % 360f;
            if (normalized < 0f)
                normalized += 360f;
            return normalized;
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
}
