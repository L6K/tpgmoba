namespace Enigma.Character
{
    /// <summary>
    /// ジャングラー(中立狩り担当ロール)が巡回・キャンプ狩りを打ち切ってマクロ行動へ
    /// 合流すべきかを決める純粋ロジック。UnityEngine 非依存。EnemyChampionAI は
    /// 入出力の配線のみを担う(Humble Object)。
    ///
    /// 根本原因の是正:
    /// 旧実装はボス討伐への合流を「ボス残HP &lt; BossCommitHpFraction(0.6)」でのみ許可して
    /// いたため、満HPで湧いた直後のボスには誰も着手できず、ジャングラーが永久にキャンプを
    /// 狩り続けるチキン&amp;エッグのデッドロックだった(誰かが先にボスを削らないと合流しない、
    /// しかし誰も削らないので永遠に条件が成立しない)。結果として直近シムではボスがほぼ
    /// 手つかずになっていた。ボスがアクティブ(湧いている)なら満HPでも合流させ、討伐の
    /// 口火をジャングラーに切らせる。
    ///
    /// レーナーの参加/離脱判断は BotMacroDecisionModel(GroupForObjective/Retreat/Defend)が
    /// 既に担うため、ここでは重複させない。本クラスはジャングラーの「キャンプを捨てて
    /// 合流するか」という、従来 EnemyChampionAI にインラインで埋まっていた判断のみを扱う。
    /// </summary>
    public static class ObjectiveMacroLogic
    {
        /// <summary>
        /// ジャングラーがキャンプ狩り/巡回を打ち切って、現在のマクロ行動へ合流すべきか。
        /// </summary>
        /// <param name="macro">直近 Sense で算出済みのマクロ判断。</param>
        /// <param name="bossActive">
        /// ボス(エニグマ・コア)がアクティブ(湧いていて生存)か。
        /// GroupForObjective でも集合先のボスが不在/死亡なら合流しない(巡回を続ける)。
        /// </param>
        /// <returns>
        /// - CloseOutSiege(閉幕プッシュ): 常に合流(従来どおり。ボス有無に依らない)。<br/>
        /// - GroupForObjective(ボス集合): ボスがアクティブなら残HPに依らず合流。<br/>
        /// - それ以外(Farm/Push/Retreat/Defend): 合流しない。
        /// </returns>
        public static bool JunglerShouldAbandonCamp(BotMacroAction macro, bool bossActive)
        {
            if (macro == BotMacroAction.CloseOutSiege) return true;
            if (macro == BotMacroAction.GroupForObjective) return bossActive;
            return false;
        }

        // ── ボス討伐コミット(問題B: 削り途中の離脱でボスが全快リセットする不具合の是正) ──
        //
        // 症状: ジャングラーがボスに着手して削っても、BotMacroDecisionModel.Decide が数秒ごとに
        // 再評価され GroupForObjective→Farm/CloseOutSiege/Defend へ入れ替わる。マクロ自体が
        // GroupForObjective を離れるため、ApplyMacroOverride 内の交戦継続判定(hpBelowCommitLine 等)
        // に到達すらせず、ボス残 132 まで削って離脱→ボス全快、を繰り返していた。
        //
        // 是正: 一度ボスに着手したら(ボスが十分削れていて=BossCommitHpFraction 未満、かつ至近で
        // 交戦可能)、Retreat(低HP)かボス消滅まで GroupForObjective に固定する「コミット・ラッチ」を
        // 設ける。しきい値は既存の BotMacroDecisionModel.BossCommitHpFraction を再利用し重複を作らない
        // (ApplyMacroOverride の交戦継続判定と同じライン。あちらは「敵接近で離脱しない」ためのフレーム内
        // 判定、こちらは「マクロが GroupForObjective を離れない」ためのラッチで責務が分かれる)。

        /// <summary>
        /// ボス討伐コミットのラッチ状態を1ステップ進める。
        /// </summary>
        /// <param name="alreadyCommitted">前フレームのコミット状態(ラッチ)。</param>
        /// <param name="bossActive">ボスが生存し交戦対象として有効か(死亡/消滅で false → 解除)。</param>
        /// <param name="bossHpFraction">ボス残HP割合(0..1)。開始判定にのみ使用。</param>
        /// <param name="selfCanFight">
        /// 自分が交戦継続可能か(= Decide が Retreat を返していない)。false なら即解除(Retreat 例外)。
        /// </param>
        /// <param name="nearObjective">ボス至近(交戦圏内)に居るか。コミット「開始」条件にのみ使用。</param>
        /// <returns>更新後のコミット状態。</returns>
        public static bool NextBossCommit(
            bool alreadyCommitted, bool bossActive, float bossHpFraction,
            bool selfCanFight, bool nearObjective)
        {
            if (!bossActive) return false;    // ボス死亡/消滅 → コミット解除
            if (!selfCanFight) return false;  // 低HP撤退 → コミット解除(Retreat 例外)
            if (alreadyCommitted) return true; // 維持(ApplyBossCommit がマクロをボスへ引き戻す)
            // 開始: ボスが押し切りライン未満まで削れていて、かつ自分が至近で交戦できるとき。
            return nearObjective && bossHpFraction < BotMacroDecisionModel.BossCommitHpFraction;
        }

        /// <summary>
        /// コミット中はマクロを GroupForObjective に固定する(Decide の再評価による離脱を止める)。
        /// 非コミット時は Decide の結果をそのまま返す。Retreat はコミット解除側で弾かれるため
        /// ここに Retreat が渡ることはない(=安全に GroupForObjective を返してよい)。
        /// </summary>
        public static BotMacroAction ApplyBossCommit(BotMacroAction decided, bool committed)
        {
            return committed ? BotMacroAction.GroupForObjective : decided;
        }
    }
}
