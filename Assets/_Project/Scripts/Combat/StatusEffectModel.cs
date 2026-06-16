using System;
using System.Collections.Generic;

namespace Enigma.Combat
{
    public sealed class StatusEffectModel
    {
        private readonly List<SlowEffect> _slows = new List<SlowEffect>();
        // ヘイスト（移動加速）。slow と独立に最強値を採用し、乗算で速度に乗る。
        private readonly List<SlowEffect> _hastes = new List<SlowEffect>();
        private float _stunRemaining;
        private float _rootRemaining;

        public bool IsStunned => _stunRemaining > 0f;
        public bool IsRooted => _rootRemaining > 0f;
        public bool IsSlowed => _slows.Count > 0;
        public bool IsHasted => _hastes.Count > 0;
        public bool CanMove => !IsStunned && !IsRooted;
        public bool CanAct => !IsStunned;
        // 減速(最大1.0)に加速(1+haste)を乗算。ルート/スタン時は別途 CanMove で停止する。
        public float MoveSpeedMultiplier => Clamp01(1f - GetStrongestSlow()) * (1f + GetStrongestHaste());

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

        // 移動加速を付与する。strength は加算割合（0.2 = +20%）。
        public void ApplyHaste(float strength, float duration)
        {
            if (duration <= 0f || strength <= 0f)
            {
                return;
            }

            _hastes.Add(new SlowEffect(strength, duration));
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

            changed |= TickTimedList(_slows, deltaTime);
            changed |= TickTimedList(_hastes, deltaTime);

            if (changed)
            {
                Changed?.Invoke();
            }
        }

        // strength+remaining のリストを減衰させ、満了分を除去する。変化があれば true。
        private static bool TickTimedList(List<SlowEffect> list, float deltaTime)
        {
            bool changed = false;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                SlowEffect e = list[i];
                e.Remaining -= deltaTime;
                if (e.Remaining <= 0f)
                {
                    list.RemoveAt(i);
                    changed = true;
                }
                else
                {
                    list[i] = e;
                }
            }
            return changed;
        }

        public void Clear()
        {
            if (!IsStunned && !IsRooted && _slows.Count == 0 && _hastes.Count == 0)
            {
                return;
            }

            _stunRemaining = 0f;
            _rootRemaining = 0f;
            _slows.Clear();
            _hastes.Clear();
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

        private float GetStrongestHaste()
        {
            float strongest = 0f;
            for (int i = 0; i < _hastes.Count; i++)
            {
                strongest = Math.Max(strongest, _hastes[i].Strength);
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
