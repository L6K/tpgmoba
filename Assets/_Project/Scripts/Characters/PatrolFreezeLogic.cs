namespace Enigma.Character
{
    /// <summary>
    /// ジャングラーの巡回(Farm/Push)が物理的に進まなくなる「凍結」を検知するための純粋ロジック。
    /// UnityEngine 非依存。EnemyChampionAI は位置差分と経過時間を渡すだけ(Humble Object)。
    ///
    /// 根本原因(問題A):
    /// Farm の移動は StepAlongPath が _waypoints[_waypointIndex] へ向かうだけで、GroupForObjective/
    /// Defend の MoveDirectlyToward でルート外(例: ボスピット)へ引き込まれると _waypointIndex が
    /// 古いまま残る。復帰時の目標がチョーク壁の反対側など「到達不能な前方ノード」だと、
    /// UpdateStuckEscape は前方(min(near+1,last))へしか振り直さないため同じ到達不能ノードを選び続け、
    /// 壁沿いに接線移動するだけで半径方向へ一切進まない(実測: r=41 で957秒静止)。前方一択の巡回には
    /// 「到達可能な次の目的地へ必ず抜ける終端」が無かった。
    ///
    /// 是正:
    /// 実移動量ベースの番犬を追加する。巡回マクロ中に一定時間ほとんど動けていなければ凍結とみなし、
    /// 呼び側が最寄りウェイポイントへ再アンカーしつつ巡回方向を反転させる(=引き込まれてきた=到達可能な
    /// 側へ必ず向かう)。FreezeTimeout ごとに必ず方向が切り替わるため、凍結状態は持続し得ない。
    /// </summary>
    public static class PatrolFreezeLogic
    {
        /// <summary>この距離以上動けていれば「進捗あり」としてアンカーをリセットする(m)。</summary>
        public const float FreezeMoveEpsilon = 0.5f;

        /// <summary>進捗が無い状態がこの秒数続いたら凍結発動とみなす。</summary>
        public const float FreezeTimeout = 3f;

        /// <summary>脱出オーバーライドの有効時間(秒)。この間はマクロの専用挙動より脱出目標への移動を優先する。</summary>
        public const float FreezeEscapeDuration = 4f;

        /// <summary>脱出目標へこの距離(m)まで近づいたら到達とみなし、通常制御へ返す。</summary>
        public const float FreezeEscapeArriveRadius = 2f;

        /// <summary>
        /// レーナーはこの水平距離(中心=マップ原点から)より内側=ジャングル内でのみ番犬を有効化する。
        /// レーン上/敵ベース前(r&gt;=54)の停滞は正当(終端保持)なので従来どおり除外する。
        /// ジャングル⇔レーンの壁帯(r54〜55.5)の内側を「ジャングル」とみなす境界。
        /// </summary>
        public const float LanerJungleRadius = 54f;

        /// <summary>
        /// アンカー位置からの実移動が閾値未満のまま FreezeTimeout 秒続いたら凍結とみなす。
        /// movedSinceAnchor が閾値以上なら false(=呼び側はアンカーを現在地・現在時刻へ更新する)。
        /// </summary>
        public static bool IsFrozen(float movedSinceAnchor, float elapsedSinceAnchor)
        {
            if (movedSinceAnchor >= FreezeMoveEpsilon) return false;
            return elapsedSinceAnchor >= FreezeTimeout;
        }

        /// <summary>
        /// このBotに凍結番犬を適用してよいか(交戦/攻囲/中立狩り等の意図した静止の除外は呼び側で済ませた前提)。
        /// ジャングラーは経路構造上どこでも詰まり得るため常に有効。
        /// レーナーはジャングル内(中心からの水平距離 &lt; LanerJungleRadius)へ引き込まれて詰まったときのみ有効化し、
        /// レーン上・敵ベース前(r&gt;=54)の正当な停滞には介入しない。
        /// </summary>
        public static bool WatchdogApplies(bool isJungler, float distFromCenter)
        {
            if (isJungler) return true;
            return distFromCenter < LanerJungleRadius;
        }

        /// <summary>
        /// マクロ・ロール・位置から凍結番犬の適用可否を判定する(交戦/攻囲/中立狩り等の実行時除外は呼び側)。
        /// ジャングラー: 従来どおり Farm/Push のみ(挙動不変)。
        /// レーナー: ジャングル内(r&lt;54)では Farm/Push に加えて Retreat 中も対象にする。
        /// 低HPのレーナーが壁・チョークで詰まると Farm/Retreat を往復し、Retreat を除外すると
        /// 往復のたびにアンカーが無効化されて FreezeTimeout を積み上げられず凍結が持続する
        /// (実測: Blue レーナーがジャングル内で462秒静止)。Retreat を含めればアンカーが往復をまたいで
        /// 持続し、無進捗を検知できる。
        /// </summary>
        public static bool WatchdogEligible(bool isJungler, BotMacroAction macro, float distFromCenter)
        {
            if (!WatchdogApplies(isJungler, distFromCenter)) return false;
            if (isJungler)
                return macro == BotMacroAction.Farm || macro == BotMacroAction.Push;
            return macro == BotMacroAction.Farm
                || macro == BotMacroAction.Push
                || macro == BotMacroAction.Retreat;
        }

        /// <summary>
        /// レーナーが Retreat 中にジャングル内で凍結したときの再アンカー先ウェイポイント index。
        /// レーン経路は index 0 = 自軍ベース開口(両チームとも。青側は Reverse 済み配列)なので、
        /// 低い index ほど自陣側。最寄りノードから自陣側へ1つ寄せて返し、Retreat の「自陣へ退く」意図を
        /// 尊重する(敵側へ送り返さない)。最寄りが既に自陣端(0)ならそのまま 0。
        /// </summary>
        public static int LanerRetreatRecoveryIndex(int nearestIndex, int waypointCount)
        {
            if (waypointCount <= 0) return 0;
            int idx = ClampIndex(nearestIndex, waypointCount);
            return idx > 0 ? idx - 1 : 0;
        }

        /// <summary>
        /// 脱出オーバーライドの目標ウェイポイント index を選ぶ。
        /// MoveDirectlyToward で直進する具体的な到達点として使う(ウェイポイント歩行に依存しないため、
        /// Retreat/GroupForObjective/Defend など専用挙動のマクロでも効く)。
        ///
        /// base = 最寄りノード(retreatBias なら自陣側へ1寄せた LanerRetreatRecoveryIndex)。
        /// attempt(発火回数, 0起点)ごとに自陣方向(index 減少)へ1つずつずらし、同じ壁で塞がれた目標に固執せず
        /// 別ノードへ切り替える。index 0 = 自軍ベース開口(退避の安全方向)でクランプする。
        /// </summary>
        public static int EscapeTargetIndex(int nearestIndex, int waypointCount, bool retreatBias, int attempt)
        {
            if (waypointCount <= 0) return 0;
            int idx = retreatBias
                ? LanerRetreatRecoveryIndex(nearestIndex, waypointCount)
                : ClampIndex(nearestIndex, waypointCount);
            if (attempt > 0) idx -= attempt;
            return ClampIndex(idx, waypointCount);
        }

        private static int ClampIndex(int idx, int waypointCount)
        {
            if (idx < 0) return 0;
            if (idx > waypointCount - 1) return waypointCount - 1;
            return idx;
        }
    }
}
