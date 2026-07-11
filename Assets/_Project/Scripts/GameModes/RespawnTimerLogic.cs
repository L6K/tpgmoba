namespace Enigma.GameModes
{
    // 試合経過によるリスポーン遅延（純粋ロジック、UnityEngine 非依存）。
    // Bot 戦がオーバータイムへ膠着するのを防ぐため、時間とともにデスタイマーを伸ばす。
    public static class RespawnTimerLogic
    {
        // 逓増開始までの猶予（秒）。これ以前は基本遅延のまま。
        public const float GraceSeconds = 300f;

        // 基本リスポーン遅延（秒）。
        public const float BaseDelay = 5f;

        // 猶予経過後、1 分ごとに加算する遅延（秒）。
        public const float PerMinuteBonus = 1.5f;

        // 上限遅延（秒）。
        public const float MaxDelay = 20f;

        /// <summary>
        /// 経過秒数に対するリスポーン遅延を返す。
        /// 300 秒まで 5.0、以降 1 分ごとに +1.5、上限 20.0。負値は基本遅延として安全に扱う。
        /// </summary>
        public static float Delay(float timeSec)
        {
            if (timeSec <= GraceSeconds) return BaseDelay;

            float minutesAfterGrace = (timeSec - GraceSeconds) / 60f;
            float delay = BaseDelay + minutesAfterGrace * PerMinuteBonus;

            return delay > MaxDelay ? MaxDelay : delay;
        }
    }
}
