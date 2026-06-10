using System;

namespace Enigma.Combat
{
    public sealed class HealthModel
    {
        public float CurrentHp { get; private set; }
        public float MaxHp { get; private set; }
        public bool IsDead => CurrentHp <= 0f;

        // current, max
        public event Action<float, float> Changed;
        public event Action Died;

        private bool _diedFired;

        public HealthModel(float maxHp)
        {
            MaxHp = maxHp;
            CurrentHp = maxHp;
        }

        public void TakeDamage(float amount)
        {
            if (_diedFired) return;

            CurrentHp = Math.Max(0f, CurrentHp - amount);
            Changed?.Invoke(CurrentHp, MaxHp);

            if (CurrentHp <= 0f && !_diedFired)
            {
                _diedFired = true;
                Died?.Invoke();
            }
        }

        public void Revive()
        {
            _diedFired = false;
            CurrentHp = MaxHp;
            Changed?.Invoke(CurrentHp, MaxHp);
        }
    }
}
