using UnityEngine;

namespace Enigma.Ability
{
    public enum SkillTargeting { Directional, GroundAoe, Targeted }

    [CreateAssetMenu(fileName = "Skill_", menuName = "Enigma/Skill Definition")]
    public sealed class SkillDefinition : ScriptableObject
    {
        public string        SkillName;
        public SkillTargeting Targeting;
        public float         Damage;
        public float         Range;
        public float         Radius;          // GroundAoe のみ使用
        public float         CooldownSeconds;
        public float         ProjectileSpeed; // Directional のみ使用
        public float         WindupSeconds   = 0.2f;
        public float         RecoverySeconds = 0.35f;
    }
}
