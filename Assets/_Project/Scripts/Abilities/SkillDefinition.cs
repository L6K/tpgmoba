using UnityEngine;

namespace Enigma.Ability
{
    // SelfAoe=自身中心の範囲攻撃(即時) / TeamAlly=自チーム全体への支援(即時)
    public enum SkillTargeting { Directional, GroundAoe, Targeted, TargetedAlly, SelfAoe, TeamAlly }

    [CreateAssetMenu(fileName = "Skill_", menuName = "Enigma/Skill Definition")]
    public sealed class SkillDefinition : ScriptableObject
    {
        public string        SkillName;
        [TextArea(2, 4)]
        public string        Description;
        public SkillTargeting Targeting;
        public float         Damage;
        public float         Range;
        public float         Radius;          // GroundAoe のみ使用
        public float         CooldownSeconds;
        public float         ProjectileSpeed; // Directional のみ使用
        public float         WindupSeconds   = 0.2f;
        public float         RecoverySeconds = 0.35f;

        // 命中した敵に付与するスタン秒。0=なし
        public float StunDuration;
        // 命中した敵に付与するルート(移動不能)秒。0=なし
        public float RootDuration;
        // スロウの強さ(0〜1, 例: 0.3=30%減速)。0=なし
        public float SlowStrength;
        // スロウの持続秒。0=なし
        public float SlowDuration;
        // 付与するシールド量(HP)。0=なし
        public float ShieldAmount;
        // シールドの持続秒。0=なし
        public float ShieldDuration;
        // 回復するHP量。0=なし
        public float HealAmount;
        // ダッシュ距離(m)。0=なし
        public float DashDistance;
    }
}
