using System;
using System.Collections.Generic;

namespace Enigma.Data
{
    public enum RelicEffect
    {
        StartShield,
        MoveSpeedOnKill,
        NeutralDamage,
        CooldownReduction,
        MaxHpBonus
    }

    public readonly struct Relic
    {
        public readonly string Id;
        public readonly RelicEffect Effect;
        public readonly float Magnitude;

        public Relic(string id, RelicEffect effect, float magnitude)
        {
            Id = id;
            Effect = effect;
            Magnitude = magnitude;
        }
    }

    public sealed class RelicLoadoutModel
    {
        private readonly Dictionary<string, Relic> _catalog = new Dictionary<string, Relic>(StringComparer.Ordinal);
        private readonly List<string> _selectedIds = new List<string>();
        private readonly int _maxSlots;

        public RelicLoadoutModel(IReadOnlyList<Relic> catalog, int maxSlots = 3)
        {
            _maxSlots = maxSlots < 1 ? 1 : maxSlots;

            if (catalog == null)
                return;

            for (int i = 0; i < catalog.Count; i++)
            {
                Relic relic = catalog[i];
                if (!string.IsNullOrEmpty(relic.Id))
                    _catalog[relic.Id] = relic;
            }
        }

        public int SelectedCount => _selectedIds.Count;

        public bool TrySelect(string relicId)
        {
            if (string.IsNullOrEmpty(relicId))
                return false;

            if (!_catalog.ContainsKey(relicId) || IsSelected(relicId) || _selectedIds.Count >= _maxSlots)
                return false;

            _selectedIds.Add(relicId);
            return true;
        }

        public bool Deselect(string relicId)
        {
            if (string.IsNullOrEmpty(relicId))
                return false;

            return _selectedIds.Remove(relicId);
        }

        public bool IsSelected(string relicId)
        {
            if (string.IsNullOrEmpty(relicId))
                return false;

            return _selectedIds.Contains(relicId);
        }

        public IReadOnlyList<Relic> Selected()
        {
            var selected = new List<Relic>(_selectedIds.Count);
            for (int i = 0; i < _selectedIds.Count; i++)
                selected.Add(_catalog[_selectedIds[i]]);

            return selected;
        }

        public IReadOnlyDictionary<RelicEffect, float> AggregateEffects()
        {
            var effects = new Dictionary<RelicEffect, float>();
            for (int i = 0; i < _selectedIds.Count; i++)
            {
                Relic relic = _catalog[_selectedIds[i]];
                effects.TryGetValue(relic.Effect, out float current);
                effects[relic.Effect] = current + relic.Magnitude;
            }

            return effects;
        }

        public void Clear()
        {
            _selectedIds.Clear();
        }
    }
}
