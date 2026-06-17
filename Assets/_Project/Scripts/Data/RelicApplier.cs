using System.Collections.Generic;
using Enigma.Ability;
using Enigma.Combat;

namespace Enigma.Data
{
    /// <summary>
    /// 選択済みレリックの集約効果をプレイヤーの各コンポーネントへ適用する。
    /// 試合開始時(MatchBootstrap)・サンドボックスから呼ばれる。
    /// 現状は開始時に効く3効果（最大HP / 開始シールド / CDR）のみ適用する。
    /// </summary>
    public static class RelicApplier
    {
        // 開始時シールドの持続秒（MOBA 慣例で開幕のみ。十分長く取って実質開幕保護にする）。
        public const float StartShieldDuration = 60f;

        public static void ApplyIds(IReadOnlyList<string> selectedIds, HealthModel health,
            SkillCaster caster, UnityEngine.GameObject player = null)
        {
            if (selectedIds == null || selectedIds.Count == 0) return;

            var loadout = new RelicLoadoutModel(RelicCatalog.Relics());
            for (int i = 0; i < selectedIds.Count; i++)
                loadout.TrySelect(selectedIds[i]);

            Apply(loadout.AggregateEffects(), health, caster, player);
        }

        public static void Apply(IReadOnlyDictionary<RelicEffect, float> effects, HealthModel health,
            SkillCaster caster, UnityEngine.GameObject player = null)
        {
            if (effects == null) return;

            if (health != null
                && effects.TryGetValue(RelicEffect.MaxHpBonus, out float hp) && hp > 0f)
                health.AddMaxHp(hp); // 生存中は CurrentHp も同量上がる

            if (health != null
                && effects.TryGetValue(RelicEffect.StartShield, out float shield) && shield > 0f)
                health.AddShield(shield, StartShieldDuration);

            if (caster != null
                && effects.TryGetValue(RelicEffect.CooldownReduction, out float cdr) && cdr > 0f)
                caster.SetCooldownReduction(cdr);

            // 遅延・条件付き効果は値だけプレイヤーへ置き、各フックが読む。
            // キル時加速→KillFeedDirector、中立与ダメ増→DamageUtility。
            if (player != null)
            {
                effects.TryGetValue(RelicEffect.MoveSpeedOnKill, out float msok);
                effects.TryGetValue(RelicEffect.NeutralDamage, out float neutral);
                var pre = PlayerRelicEffects.GetOrAdd(player);
                pre.SetMoveSpeedOnKill(msok);
                pre.SetNeutralDamageBonus(neutral);
            }
        }
    }
}
