namespace Enigma.Combat
{
    // 近接ブルーザーの対構造物ボーナス(純ロジック)。
    // 「OT時点の盤面優勢=勝敗」メタでは CS/ポークが盤面に直結する一方、近接のキルは
    // 勝ちに変換されず、近接3キャラ(thorne/garon/veil)が4バッチ連続で勝率50%未満に沈んだ。
    // 近接に「攻城役」という盤面への回路を与えるクラスレベルの是正(2026-07-06 ユーザー承認)。
    public static class MeleeSiegeLogic
    {
        // AutoAttack.MeleeRangeThreshold と同じ閾値(射程7以下=近接: garon 3.5 / veil 4 / thorne 3.5)
        public const float MeleeRangeThreshold = 7f;

        // 近接の対構造物ダメージ倍率
        public const float StructureMultiplier = 1.5f;

        /// <summary>攻撃者の AA 射程から対構造物倍率を返す(遠隔は等倍)。</summary>
        public static float Multiplier(float attackRange)
        {
            return attackRange <= MeleeRangeThreshold ? StructureMultiplier : 1f;
        }
    }
}
