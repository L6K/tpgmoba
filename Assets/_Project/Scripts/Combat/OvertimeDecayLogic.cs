namespace Enigma.Combat
{
    // オーバータイム(サドンデス)の減衰計算。Unity 非依存の純ロジック。
    // 対称な AI 同士の試合はレーン中央で永久均衡しうるため、一定時間経過後に
    // 構造物(タワー/タイタン)を毎秒減衰させて決着を構造的に保証する。
    public static class OvertimeDecayLogic
    {
        public const float DefaultOvertimeStartSeconds = 1200f; // 20分

        // 毎秒の減衰率(最大HP比)。タワー(600-800)は約100秒、タイタン(2500)も
        // 同率なので約100秒で崩壊し、オーバータイム突入から2分弱で必ず決着する
        public const float DecayFractionPerSecond = 0.01f;

        /// <summary>elapsedSeconds 時点での1秒あたり減衰量を返す(開始前は0)。</summary>
        public static float DamagePerSecond(float maxHp, float elapsedSeconds,
            float overtimeStartSeconds = DefaultOvertimeStartSeconds)
        {
            if (maxHp <= 0f) return 0f;
            if (elapsedSeconds < overtimeStartSeconds) return 0f;
            return maxHp * DecayFractionPerSecond;
        }
    }
}
