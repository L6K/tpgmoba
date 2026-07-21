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
    }
}
