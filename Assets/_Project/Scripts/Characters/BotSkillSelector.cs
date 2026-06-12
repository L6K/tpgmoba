namespace Enigma.Character
{
    // ボットが「どのスキルスロットを撃つか」を決める純ロジック（MonoBehaviour 非依存）。
    // スロットの意味は固定: 0=Q(方向), 1=E(地点AoE), 2=R(対象指定)。
    // 優先度は R(ターゲット低HP時のみ) > Q > E。射程内かつ CD 準備済みのみ候補。
    public static class BotSkillSelector
    {
        // R を撃つ閾値。これ未満のターゲット HP 割合でのみ R を解禁する。
        private const float UltimateHpThreshold = 0.4f;

        public readonly struct SlotState
        {
            public readonly bool Ready;   // クールダウン準備済みか
            public readonly float Range;  // スキル射程

            public SlotState(bool ready, float range)
            {
                Ready = ready;
                Range = range;
            }
        }

        /// <summary>
        /// 撃つべきスロット番号を返す。撃てるものが無ければ -1。
        /// </summary>
        /// <param name="q">スロット0(Q/方向)の状態</param>
        /// <param name="e">スロット1(E/地点AoE)の状態</param>
        /// <param name="r">スロット2(R/対象指定)の状態</param>
        /// <param name="targetDistance">自分→ターゲットの距離</param>
        /// <param name="targetHpRatio">ターゲットの現在HP割合(0..1)</param>
        public static int Select(
            SlotState q, SlotState e, SlotState r,
            float targetDistance, float targetHpRatio)
        {
            // R はターゲットが瀕死(HP<40%)のときだけ最優先で撃つ
            if (r.Ready && targetHpRatio < UltimateHpThreshold && targetDistance <= r.Range)
                return 2;

            // 次点 Q（方向スキル）
            if (q.Ready && targetDistance <= q.Range)
                return 0;

            // 最後 E（地点AoE）。射程内であれば Radius 距離条件は問わない
            if (e.Ready && targetDistance <= e.Range)
                return 1;

            return -1;
        }
    }
}
