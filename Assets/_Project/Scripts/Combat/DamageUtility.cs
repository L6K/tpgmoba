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
        // 対象を加味する版。攻撃者のチームバフ等を適用したうえで、対象が中立なら
        // 攻撃者の所持レリック「中立与ダメ増」を乗算する（プレイヤーのみ・命中時に呼ぶ）。
        public static float ApplyTeamBuff(float baseDamage, GameObject attacker, GameObject target)
        {
            float dmg = ApplyTeamBuff(baseDamage, attacker);
            if (attacker == null || target == null) return dmg;

            var targetTag = target.GetComponentInParent<TeamTag>();
            if (targetTag == null || targetTag.Team != TeamId.Neutral) return dmg;

            var relics = attacker.GetComponentInParent<Enigma.Data.PlayerRelicEffects>();
            if (relics != null && relics.NeutralDamageBonus > 0f)
                dmg *= 1f + relics.NeutralDamageBonus;

            return dmg;
        }

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
