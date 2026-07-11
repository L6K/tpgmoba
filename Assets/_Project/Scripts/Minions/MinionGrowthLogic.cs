namespace Enigma.Minion
{
    // 試合経過によるミニオン強化倍率（純粋ロジック、UnityEngine 非依存）。
    // Bot 戦がオーバータイムへ膠着するのを防ぐため、時間とともに HP と攻撃力を底上げする。
    public static class MinionGrowthLogic
    {
        // 強化開始までの猶予（秒）。これ以前は等倍。
        public const float GraceSeconds = 300f;

        // 猶予経過後、1 分ごとに加算する倍率。
        public const float PerMinuteBonus = 0.08f;

        // 上限倍率（+80%）。
        public const float MaxMultiplier = 1.8f;

        /// <summary>
        /// 経過秒数に対する強化倍率を返す。
        /// 300 秒まで 1.0、以降 1 分ごとに +8%、上限 1.8。負値は等倍として安全に扱う。
        /// </summary>
        public static float Multiplier(float timeSec)
        {
            if (timeSec <= GraceSeconds) return 1f;

            float minutesAfterGrace = (timeSec - GraceSeconds) / 60f;
            float mult = 1f + minutesAfterGrace * PerMinuteBonus;

            return mult > MaxMultiplier ? MaxMultiplier : mult;
        }
    }
}
