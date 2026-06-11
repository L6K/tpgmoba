using UnityEngine;
using Enigma.Core;

namespace Enigma.Combat
{
    // 攻撃者のチームバフを加味したダメージ計算の薄いヘルパー。
    // GameServices 参照を各攻撃実装に分散させないために一点に集約する。
    public static class DamageUtility
    {
        public static float ApplyTeamBuff(float baseDamage, GameObject attacker)
        {
            if (attacker == null) return baseDamage;
            var teamTag = attacker.GetComponentInParent<TeamTag>();
            if (teamTag == null) return baseDamage;
            if (teamTag.Team == TeamId.Neutral) return baseDamage;

            var buffs = GameServices.TeamBuffs;
            if (buffs == null) return baseDamage;

            return baseDamage * buffs.GetDamageMultiplier(teamTag.Team, Time.time);
        }
    }
}
