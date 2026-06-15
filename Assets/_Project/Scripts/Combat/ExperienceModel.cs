using System;

namespace Enigma.Combat
{
    // レベルアップ閾値 80+40*(Lv-1) はConfluence「07_ジャングルキャンプとXPシステム」準拠
    public sealed class ExperienceModel
    {
        public const int MaxLevel = 10;

        public int   Level      { get; private set; } = 1;
        public float CurrentXp  { get; private set; } = 0f;

        // 最大レベル時は意味を持たないが計算式は維持する
        public float XpToNext => 80f + 40f * (Level - 1);

        public event Action<int> LevelChanged;

        public void AddXp(float amount)
        {
            if (Level >= MaxLevel) return;

            CurrentXp += amount;

            // 余剰XPを繰り越しながら連続レベルアップに対応
            while (Level < MaxLevel && CurrentXp >= XpToNext)
            {
                CurrentXp -= XpToNext;
                Level++;
                LevelChanged?.Invoke(Level);
            }

            // 最大レベル到達後は超過分を捨てる
            if (Level >= MaxLevel)
                CurrentXp = 0f;
        }
    }
}
