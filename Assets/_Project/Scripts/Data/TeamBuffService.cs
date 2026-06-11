using System.Collections.Generic;
using Enigma.Combat;

namespace Enigma.Data
{
    // チームごとのダメージバフ（期限管理）を持つ純粋 C# サービス
    public sealed class TeamBuffService : ITeamBuffService
    {
        private readonly struct BuffEntry
        {
            public readonly float Multiplier;
            public readonly float ExpiresAt;
            public BuffEntry(float multiplier, float expiresAt)
            {
                Multiplier = multiplier;
                ExpiresAt  = expiresAt;
            }
        }

        private readonly Dictionary<TeamId, BuffEntry> _buffs = new();

        public void GrantDamageBuff(TeamId team, float multiplier, float durationSeconds, float now)
        {
            _buffs[team] = new BuffEntry(multiplier, now + durationSeconds);
        }

        public float GetDamageMultiplier(TeamId team, float now)
        {
            if (_buffs.TryGetValue(team, out var entry) && now < entry.ExpiresAt)
                return entry.Multiplier;
            return 1f;
        }

        public float GetRemainingSeconds(TeamId team, float now)
        {
            if (_buffs.TryGetValue(team, out var entry) && now < entry.ExpiresAt)
                return entry.ExpiresAt - now;
            return 0f;
        }
    }
}
