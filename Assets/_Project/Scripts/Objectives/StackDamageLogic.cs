namespace Enigma.Objective
{
    // 頭割りダメージ計算の純粋関数（テスト対象）
    public static class StackDamageLogic
    {
        /// <summary>
        /// 総ダメージを targetCount 人で頭割りした 1 人分の値を返す。
        /// count &lt;= 0 のときは totalDamage をそのまま返す（呼び出し側が count &gt;= 1 を保証する想定）。
        /// </summary>
        public static float DamagePerTarget(float totalDamage, int targetCount)
        {
            if (targetCount <= 0) return totalDamage;
            return totalDamage / targetCount;
        }
    }
}
