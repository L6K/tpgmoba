namespace Enigma.Character
{
    /// <summary>
    /// GroupForObjective 中、エンゲージ圏(BossEngageRange)へ一定時間到達できない場合に
    /// 一時的にコア集合を諦めさせるための純粋ロジック。壁ジャム等でコア集合へ恒久的に
    /// 詰む(棒立ちで動かなくなる)実測を防ぐフォールバックであり、経路自体の改善はスコープ外。
    /// 時刻は Unity 非依存にするため呼び側(EnemyChampionAI)が Time.time を渡す。
    /// </summary>
    public static class ObjectiveGiveUpLogic
    {
        /// <summary>エンゲージ圏に居ない状態がこの秒数続いたらギブアップ判定を出す。</summary>
        public const float StuckTimeout = 20f;
        /// <summary>ギブアップ後、GroupForObjective を選ばせない期間。</summary>
        public const float GiveUpCooldown = 30f;

        /// <summary>
        /// 経過時刻を反映した「エンゲージ圏未到達の継続開始時刻」を返す。
        /// 圏内に到達したら未到達継続はリセット(NaN)する。未到達が続く場合は開始時刻を保持する。
        /// </summary>
        public static float NextStuckSince(float currentStuckSince, bool inEngageRange, float now)
        {
            if (inEngageRange) return float.NaN;
            if (float.IsNaN(currentStuckSince)) return now;
            return currentStuckSince;
        }

        /// <summary>
        /// 未到達継続が StuckTimeout を超えたらギブアップ発動とみなす。
        /// </summary>
        public static bool ShouldGiveUp(float stuckSince, float now)
        {
            if (float.IsNaN(stuckSince)) return false;
            return now - stuckSince >= StuckTimeout;
        }

        /// <summary>
        /// ギブアップ発動時刻から GiveUpCooldown 秒以内なら GroupForObjective を選ばせない。
        /// </summary>
        public static bool IsOnCooldown(float giveUpAt, float now)
        {
            if (float.IsNaN(giveUpAt)) return false;
            return now - giveUpAt < GiveUpCooldown;
        }
    }
}
