using System.Collections.Generic;

namespace Enigma.Character
{
    /// <summary>
    /// チャンピオン対面時の交戦開始半径を計算する純ロジック（UnityEngine 非依存）。
    /// 近接キャラは AA 射程が短いため、従来は「敵チャンピオンに AA 射程まで近づかない限り
    /// 戦闘モードに入らない」構造になっており、CD明けスキルの射程（AA射程より長いことが多い）を
    /// 使う機会が実質存在しなかった。AA射程と CD明けスキルの射程のうち最大値を交戦半径として
    /// 採用することで、この非対称を解消する。
    /// </summary>
    public static class EngageRangeLogic
    {
        /// <summary>
        /// 交戦開始半径 = max(AA射程, CD明けの各スキルRangeの最大値)。
        /// CD中のスキル（撃てない）は無視する。skills が null/空なら AA射程をそのまま返す。
        /// </summary>
        public static float Effective(float attackRange, IReadOnlyList<(bool ready, float range)> skills)
        {
            float best = attackRange;
            if (skills != null)
            {
                for (int i = 0; i < skills.Count; i++)
                {
                    var s = skills[i];
                    if (s.ready && s.range > best) best = s.range;
                }
            }
            return best;
        }
    }
}
