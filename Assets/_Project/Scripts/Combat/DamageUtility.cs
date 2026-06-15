using UnityEngine;
using Enigma.Core;
using Enigma.Character;
using Enigma.GameModes;

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

            // ダメージバフの正本は ObjectiveBuffModel(Damage 種別)。倍率 = 1 + 加算magnitude。
            float damageBuffMagnitude =
                GameServices.ObjectiveBuffs?.GetMagnitude(teamTag.Team, ObjectiveBuffType.Damage, Time.time) ?? 0f;
            float damage = baseDamage * (1f + damageBuffMagnitude);

            // プレイヤーのレベルに応じたダメージ倍率を乗算
            var progression = attacker.GetComponent<PlayerProgression>();
            if (progression != null)
                damage *= progression.DamageMultiplier;

            // アイテムの攻撃力ボーナスを乗算
            var playerItems = attacker.GetComponent<PlayerItems>();
            if (playerItems != null)
                damage *= playerItems.AttackMultiplier;

            return damage;
        }
    }
}
