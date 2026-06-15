namespace Enigma.GameModes
{
    /// <summary>試合中にプレイヤーへ提示する「次の行動」ヒント。</summary>
    public enum MatchHint
    {
        Farm,             // ファームでレベル/ゴールドを稼ぐ
        PushWithMinions,  // 味方ミニオンと一緒に押す
        BackToShop,       // ゴールドが貯まったので帰還して装備更新
        Retreat,          // HP が低い、引く
        ContestObjective, // 中央オブジェクト出現中、確保へ
        ObjectiveSoon     // 中央オブジェクトまもなく出現
    }

    public readonly struct MatchHintContext
    {
        public readonly float SelfHpFraction;       // 0..1
        public readonly int   Gold;
        public readonly bool  ObjectiveActive;       // 中央オブジェクト出現中
        public readonly bool  ObjectiveWarning;      // まもなく出現(予告)
        public readonly bool  AlliedMinionsPresent;  // 近くに味方ミニオンが居る

        public MatchHintContext(float selfHpFraction, int gold, bool objectiveActive,
                                bool objectiveWarning, bool alliedMinionsPresent)
        {
            SelfHpFraction       = selfHpFraction;
            Gold                 = gold;
            ObjectiveActive      = objectiveActive;
            ObjectiveWarning     = objectiveWarning;
            AlliedMinionsPresent = alliedMinionsPresent;
        }
    }

    /// <summary>
    /// 状況から「次の行動」ヒントを1つ選ぶ純関数。優先順は安全＞好機(オブジェクト)＞経済＞前進＞ファーム。
    /// </summary>
    public static class MatchHintModel
    {
        public const float LowHpFraction    = 0.3f;   // これ未満で撤退を促す
        public const int   ShopGoldThreshold = 1000;  // これ以上で帰還購入を促す

        public static MatchHint Select(in MatchHintContext ctx)
        {
            if (ctx.SelfHpFraction < LowHpFraction) return MatchHint.Retreat;
            if (ctx.ObjectiveActive)                return MatchHint.ContestObjective;
            if (ctx.ObjectiveWarning)               return MatchHint.ObjectiveSoon;
            if (ctx.Gold >= ShopGoldThreshold)      return MatchHint.BackToShop;
            if (ctx.AlliedMinionsPresent)           return MatchHint.PushWithMinions;
            return MatchHint.Farm;
        }
    }
}
