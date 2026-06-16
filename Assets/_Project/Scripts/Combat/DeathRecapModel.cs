using System;
using System.Collections.Generic;

namespace Enigma.Combat
{
    public readonly struct DamageEvent
    {
        public readonly string SourceId;
        public readonly float Amount;
        public readonly float Time;

        public DamageEvent(string sourceId, float amount, float time)
        {
            SourceId = NormalizeSource(sourceId);
            Amount = amount;
            Time = time;
        }

        private static string NormalizeSource(string sourceId)
        {
            return string.IsNullOrEmpty(sourceId) ? "Unknown" : sourceId;
        }
    }

    public readonly struct RecapEntry
    {
        public readonly string SourceId;
        public readonly float TotalDamage;
        public readonly int HitCount;

        public RecapEntry(string sourceId, float totalDamage, int hitCount)
        {
            SourceId = string.IsNullOrEmpty(sourceId) ? "Unknown" : sourceId;
            TotalDamage = totalDamage;
            HitCount = hitCount;
        }
    }

    public sealed class DeathRecapModel
    {
        private readonly float _windowSeconds;
        private readonly int _maxEvents;
        private readonly List<DamageEvent> _events = new List<DamageEvent>();

        public DeathRecapModel(float windowSeconds = 12f, int maxEvents = 128)
        {
            _windowSeconds = windowSeconds <= 0f ? 12f : windowSeconds;
            _maxEvents = maxEvents < 1 ? 128 : maxEvents;
        }

        public void Record(string sourceId, float amount, float now)
        {
            if (amount <= 0f)
                return;

            _events.Add(new DamageEvent(sourceId, amount, now));
            while (_events.Count > _maxEvents)
                _events.RemoveAt(0);
        }

        public IReadOnlyList<RecapEntry> BuildRecap(float now)
        {
            var totals = new Dictionary<string, RecapAccumulator>(StringComparer.Ordinal);
            for (int i = 0; i < _events.Count; i++)
            {
                DamageEvent damageEvent = _events[i];
                if (!IsInWindow(damageEvent, now))
                    continue;

                if (!totals.TryGetValue(damageEvent.SourceId, out RecapAccumulator accumulator))
                    accumulator = new RecapAccumulator();

                accumulator.TotalDamage += damageEvent.Amount;
                accumulator.HitCount++;
                totals[damageEvent.SourceId] = accumulator;
            }

            var entries = new List<RecapEntry>();
            foreach (KeyValuePair<string, RecapAccumulator> pair in totals)
                entries.Add(new RecapEntry(pair.Key, pair.Value.TotalDamage, pair.Value.HitCount));

            entries.Sort(CompareEntries);
            return entries;
        }

        public float TotalInWindow(float now)
        {
            float total = 0f;
            for (int i = 0; i < _events.Count; i++)
            {
                DamageEvent damageEvent = _events[i];
                if (IsInWindow(damageEvent, now))
                    total += damageEvent.Amount;
            }

            return total;
        }

        public void Clear()
        {
            _events.Clear();
        }

        private bool IsInWindow(DamageEvent damageEvent, float now)
        {
            return now - damageEvent.Time <= _windowSeconds;
        }

        private static int CompareEntries(RecapEntry a, RecapEntry b)
        {
            int damageCompare = b.TotalDamage.CompareTo(a.TotalDamage);
            if (damageCompare != 0)
                return damageCompare;

            return string.CompareOrdinal(a.SourceId, b.SourceId);
        }

        private struct RecapAccumulator
        {
            public float TotalDamage;
            public int HitCount;
        }
    }
}
