using System;
using System.Collections.Generic;

namespace Enigma.Combat
{
    public sealed class HealthModel
    {
        private readonly List<ShieldEntry> _shields = new List<ShieldEntry>();
        private bool _diedFired;

        public float CurrentHp { get; private set; }
        public float MaxHp { get; private set; }
        public float Shield => GetShieldTotal();
        public bool IsDead => CurrentHp <= 0f;

        // current, max
        public event Action<float, float> Changed;
        public event Action Died;
        public event Action Revived;
        public event Action<float> ShieldChanged;

        public HealthModel(float maxHp)
        {
            MaxHp = maxHp;
            CurrentHp = maxHp;
        }

        public void TakeDamage(float amount)
        {
            if (_diedFired) return;
            if (amount <= 0f) return;

            float remainingDamage = amount;
            bool shieldConsumed = ConsumeShield(ref remainingDamage);
            if (shieldConsumed)
                ShieldChanged?.Invoke(Shield);

            if (remainingDamage <= 0f) return;

            CurrentHp = Math.Max(0f, CurrentHp - remainingDamage);
            Changed?.Invoke(CurrentHp, MaxHp);

            if (CurrentHp <= 0f && !_diedFired)
            {
                _diedFired = true;
                Died?.Invoke();
            }
        }

        public void AddShield(float amount, float duration)
        {
            if (amount <= 0f || duration <= 0f || IsDead)
                return;

            _shields.Add(new ShieldEntry(amount, duration));
            ShieldChanged?.Invoke(Shield);
        }

        public void Tick(float deltaTime)
        {
            if (deltaTime < 0f)
                deltaTime = 0f;

            bool removed = false;
            for (int i = _shields.Count - 1; i >= 0; i--)
            {
                ShieldEntry shield = _shields[i];
                shield.Remaining -= deltaTime;
                if (shield.Remaining <= 0f)
                {
                    _shields.RemoveAt(i);
                    removed = true;
                }
                else
                {
                    _shields[i] = shield;
                }
            }

            if (removed)
                ShieldChanged?.Invoke(Shield);
        }

        // MaxHp を増加し、生存中は同量だけ CurrentHp も回復する（アイテム装備時の即時 HP 増加）
        public void AddMaxHp(float amount)
        {
            MaxHp += amount;
            if (!IsDead)
                CurrentHp += amount;
            Changed?.Invoke(CurrentHp, MaxHp);
        }

        // 泉(ベース)回復など。死亡中は無効
        public void Heal(float amount)
        {
            if (IsDead || amount <= 0f) return;
            float before = CurrentHp;
            CurrentHp = Math.Min(MaxHp, CurrentHp + amount);
            if (CurrentHp != before)
                Changed?.Invoke(CurrentHp, MaxHp);
        }

        public void Revive()
        {
            bool hadShield = _shields.Count > 0;
            _diedFired = false;
            CurrentHp = MaxHp;
            _shields.Clear();
            Changed?.Invoke(CurrentHp, MaxHp);
            if (hadShield)
                ShieldChanged?.Invoke(0f);
            Revived?.Invoke();
        }

        private bool ConsumeShield(ref float damage)
        {
            bool consumed = false;
            for (int i = 0; i < _shields.Count && damage > 0f;)
            {
                ShieldEntry shield = _shields[i];
                float absorbed = Math.Min(shield.Amount, damage);
                shield.Amount -= absorbed;
                damage -= absorbed;
                consumed = absorbed > 0f || consumed;

                if (shield.Amount <= 0f)
                {
                    _shields.RemoveAt(i);
                }
                else
                {
                    _shields[i] = shield;
                    i++;
                }
            }

            return consumed;
        }

        private float GetShieldTotal()
        {
            float total = 0f;
            for (int i = 0; i < _shields.Count; i++)
                total += _shields[i].Amount;

            return Math.Max(0f, total);
        }

        private struct ShieldEntry
        {
            public ShieldEntry(float amount, float remaining)
            {
                Amount = amount;
                Remaining = remaining;
            }

            public float Amount { get; set; }
            public float Remaining { get; set; }
        }
    }
}
