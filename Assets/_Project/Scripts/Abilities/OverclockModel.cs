namespace Enigma.Ability
{
    public readonly struct OverclockResult
    {
        public readonly float Charge01;
        public readonly float AmpFactor;
        public readonly float HpCost;
        public readonly float ShieldCost;
        public readonly bool CanCast;

        public OverclockResult(float charge01, float ampFactor, float hpCost, float shieldCost, bool canCast)
        {
            Charge01 = charge01;
            AmpFactor = ampFactor;
            HpCost = hpCost;
            ShieldCost = shieldCost;
            CanCast = canCast;
        }
    }

    public sealed class OverclockModel
    {
        private readonly float _maxChargeSeconds;
        private readonly float _maxAmp;
        private readonly float _maxCostFraction;
        private readonly float _minHpAfter;

        public OverclockModel(float maxChargeSeconds = 1.2f, float maxAmp = 1.8f, float maxCostFraction = 0.25f, float minHpAfter = 1f)
        {
            _maxChargeSeconds = maxChargeSeconds <= 0f ? 1.2f : maxChargeSeconds;
            _maxAmp = maxAmp < 1f ? 1f : maxAmp;
            _maxCostFraction = maxCostFraction <= 0f ? 0.25f : maxCostFraction;
            _minHpAfter = minHpAfter <= 0f ? 1f : minHpAfter;
        }

        public OverclockResult Evaluate(float chargeHeldSeconds, float currentHp, float maxHp, float currentShield)
        {
            float charge01 = chargeHeldSeconds <= 0f ? 0f : Clamp01(chargeHeldSeconds / _maxChargeSeconds);
            float ampFactor = AmpAt(charge01);
            if (charge01 <= 0f)
                return new OverclockResult(0f, ampFactor, 0f, 0f, true);

            float totalCost = (maxHp <= 0f ? 0f : maxHp) * _maxCostFraction * charge01;
            float availableShield = currentShield < 0f ? 0f : currentShield;
            float shieldCost = Min(availableShield, totalCost);
            float hpCost = totalCost - shieldCost;
            bool canCast = currentHp - hpCost >= _minHpAfter;

            return new OverclockResult(charge01, ampFactor, hpCost, shieldCost, canCast);
        }

        public float AmpAt(float charge01)
        {
            float charge = Clamp01(charge01);
            return 1f + (_maxAmp - 1f) * charge;
        }

        private static float Min(float a, float b)
        {
            return a < b ? a : b;
        }

        private static float Clamp01(float value)
        {
            if (value < 0f) return 0f;
            if (value > 1f) return 1f;
            return value;
        }
    }
}
