using Enigma.Combat;

namespace Enigma.Data
{
    public interface ITeamBuffService
    {
        void  GrantDamageBuff(TeamId team, float multiplier, float durationSeconds, float now);
        float GetDamageMultiplier(TeamId team, float now);
        float GetRemainingSeconds(TeamId team, float now);
    }
}
