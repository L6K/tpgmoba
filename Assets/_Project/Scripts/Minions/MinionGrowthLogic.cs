namespace Enigma.Minion
{
    // 試合経過によるミニオン強化倍率（純粋ロジック、UnityEngine 非依存）。
    // Bot 戦がオーバータイムへ膠着するのを防ぐため、時間とともに HP と攻撃力を底上げする。
    public static class MinionGrowthLogic
    {
        // 強化開始までの猶予（秒）。これ以前は等倍。
        public const float GraceSeconds = 300f;

        // 猶予経過後、1 分ごとに加算する倍率。
        // 旧 MinionScaling(180s/+8%/上限3.0、OT時点≒1.96倍)でも自然決着ゼロだった実測を受け、
        // 「閉じる圧力」として意味を持つ値に引き上げ(OT突入時点=900sで2.5倍)。
        public const float PerMinuteBonus = 0.15f;

        // 上限倍率（+150%）。
        public const float MaxMultiplier = 2.5f;

        /// <summary>
        /// 経過秒数に対する強化倍率を返す。
        /// 300 秒まで 1.0、以降 1 分ごとに +15%、上限 2.5。負値は等倍として安全に扱う。
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
