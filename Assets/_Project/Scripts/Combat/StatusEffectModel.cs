using System;
using System.Collections.Generic;

namespace Enigma.Combat
{
    public sealed class StatusEffectModel
    {
        private readonly List<SlowEffect> _slows = new List<SlowEffect>();
        private float _stunRemaining;
        private float _rootRemaining;

        public bool IsStunned => _stunRemaining > 0f;
        public bool IsRooted => _rootRemaining > 0f;
        public bool IsSlowed => _slows.Count > 0;
        public bool CanMove => !IsStunned && !IsRooted;
        public bool CanAct => !IsStunned;
        public float MoveSpeedMultiplier => Clamp01(1f - GetStrongestSlow());

        public event Action Changed;

        public void ApplyStun(float duration)
        {
            if (duration <= 0f)
            {
                return;
            }

            float nextRemaining = Math.Max(_stunRemaining, duration);
            if (nextRemaining == _stunRemaining)
            {
                return;
            }

            _stunRemaining = nextRemaining;
            Changed?.Invoke();
        }

        public void ApplyRoot(float duration)
        {
            if (duration <= 0f)
            {
                return;
            }

            float nextRemaining = Math.Max(_rootRemaining, duration);
            if (nextRemaining == _rootRemaining)
            {
                return;
            }

            _rootRemaining = nextRemaining;
            Changed?.Invoke();
        }

        public void ApplySlow(float strength01, float duration)
        {
            if (duration <= 0f)
            {
                return;
            }

            _slows.Add(new SlowEffect(Clamp01(strength01), duration));
            Changed?.Invoke();
        }

        public void Tick(float deltaTime)
        {
            if (deltaTime < 0f)
            {
                deltaTime = 0f;
            }

            bool changed = TickHardControl(ref _stunRemaining, deltaTime);
            changed |= TickHardControl(ref _rootRemaining, deltaTime);

            for (int i = _slows.Count - 1; i >= 0; i--)
            {
                SlowEffect slow = _slows[i];
                slow.Remaining -= deltaTime;
                if (slow.Remaining <= 0f)
                {
                    _slows.RemoveAt(i);
                    changed = true;
                }
                else
                {
                    _slows[i] = slow;
                }
            }

            if (changed)
            {
                Changed?.Invoke();
            }
        }

        public void Clear()
        {
            if (!IsStunned && !IsRooted && _slows.Count == 0)
            {
                return;
            }

            _stunRemaining = 0f;
            _rootRemaining = 0f;
            _slows.Clear();
            Changed?.Invoke();
        }

        private static bool TickHardControl(ref float remaining, float deltaTime)
        {
            if (remaining <= 0f)
            {
                return false;
            }

            remaining -= deltaTime;
            if (remaining > 0f)
            {
                return false;
            }

            remaining = 0f;
            return true;
        }

        private float GetStrongestSlow()
        {
            float strongest = 0f;
            for (int i = 0; i < _slows.Count; i++)
            {
                strongest = Math.Max(strongest, _slows[i].Strength);
            }

            return strongest;
        }

        private static float Clamp01(float value)
        {
            return Math.Max(0f, Math.Min(1f, value));
        }

        private struct SlowEffect
        {
            public SlowEffect(float strength, float remaining)
            {
                Strength = strength;
                Remaining = remaining;
            }

            public float Strength { get; }
            public float Remaining { get; set; }
        }
    }
}
