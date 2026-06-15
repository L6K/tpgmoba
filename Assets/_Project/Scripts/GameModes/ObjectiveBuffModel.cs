using System;
using System.Collections.Generic;
using Enigma.Combat;

namespace Enigma.GameModes
{
    public enum ObjectiveBuffType
    {
        Damage,
        MinionPower,
        MoveSpeed,
        Shield,
        TowerWeaken
    }

    public sealed class ObjectiveBuffModel
    {
        private readonly struct BuffEntry
        {
            public readonly float Magnitude;
            public readonly float ExpiresAt;

            public BuffEntry(float magnitude, float expiresAt)
            {
                Magnitude = magnitude;
                ExpiresAt = expiresAt;
            }
        }

        private readonly Dictionary<TeamId, Dictionary<ObjectiveBuffType, List<BuffEntry>>> _buffs = new();

        public void Grant(TeamId team, ObjectiveBuffType type, float magnitude, float duration, float now)
        {
            if (magnitude <= 0f || duration <= 0f)
                return;

            List<BuffEntry> entries = GetOrCreateEntries(team, type);
            RemoveExpired(entries, now);
            entries.Add(new BuffEntry(magnitude, now + duration));
        }

        public float GetMagnitude(TeamId team, ObjectiveBuffType type, float now)
        {
            if (!TryGetEntries(team, type, out var entries))
                return 0f;

            float maxMagnitude = 0f;
            for (int i = 0; i < entries.Count; i++)
            {
                BuffEntry entry = entries[i];
                if (entry.ExpiresAt > now)
                    maxMagnitude = Math.Max(maxMagnitude, entry.Magnitude);
            }

            return maxMagnitude;
        }

        public float GetRemainingSeconds(TeamId team, ObjectiveBuffType type, float now)
        {
            if (!TryGetEntries(team, type, out var entries))
                return 0f;

            float latestExpiresAt = now;
            for (int i = 0; i < entries.Count; i++)
            {
                BuffEntry entry = entries[i];
                if (entry.ExpiresAt > now)
                    latestExpiresAt = Math.Max(latestExpiresAt, entry.ExpiresAt);
            }

            return Math.Max(0f, latestExpiresAt - now);
        }

        public IReadOnlyList<ObjectiveBuffType> GetActiveTypes(TeamId team, float now)
        {
            if (!_buffs.TryGetValue(team, out var typeEntries))
                return Array.Empty<ObjectiveBuffType>();

            var activeTypes = new List<ObjectiveBuffType>();
            foreach (KeyValuePair<ObjectiveBuffType, List<BuffEntry>> pair in typeEntries)
            {
                if (HasActiveEntry(pair.Value, now))
                    activeTypes.Add(pair.Key);
            }

            return activeTypes;
        }

        public void Clear()
        {
            _buffs.Clear();
        }

        private List<BuffEntry> GetOrCreateEntries(TeamId team, ObjectiveBuffType type)
        {
            if (!_buffs.TryGetValue(team, out var typeEntries))
            {
                typeEntries = new Dictionary<ObjectiveBuffType, List<BuffEntry>>();
                _buffs.Add(team, typeEntries);
            }

            if (!typeEntries.TryGetValue(type, out var entries))
            {
                entries = new List<BuffEntry>();
                typeEntries.Add(type, entries);
            }

            return entries;
        }

        private bool TryGetEntries(TeamId team, ObjectiveBuffType type, out List<BuffEntry> entries)
        {
            entries = null;
            if (!_buffs.TryGetValue(team, out var typeEntries))
                return false;

            return typeEntries.TryGetValue(type, out entries);
        }

        private static void RemoveExpired(List<BuffEntry> entries, float now)
        {
            for (int i = entries.Count - 1; i >= 0; i--)
            {
                if (entries[i].ExpiresAt <= now)
                    entries.RemoveAt(i);
            }
        }

        private static bool HasActiveEntry(List<BuffEntry> entries, float now)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].ExpiresAt > now)
                    return true;
            }

            return false;
        }
    }
}
