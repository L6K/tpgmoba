using UnityEngine;

namespace Enigma.UI
{
    /// <summary>
    /// HP バー目盛りの計算ロジック。MonoBehaviour に依存しない純粋 C# クラス。
    /// </summary>
    public static class HealthBarTicks
    {
        /// <summary>maxHp に応じた目盛り単位を返す。</summary>
        public static float TickUnit(float maxHp)
        {
            if (maxHp <= 500f)  return 50f;
            if (maxHp <= 2000f) return 100f;
            return 500f;
        }

        /// <summary>
        /// バー内部の目盛り本数（0 と maxHp 自身を除く）。
        /// maxHp が unit の倍数なら (maxHp/unit - 1) 本、
        /// そうでなければ floor(maxHp/unit) 本。
        /// </summary>
        public static int InnerTickCount(float maxHp)
        {
            float unit = TickUnit(maxHp);
            int divisions = Mathf.FloorToInt(maxHp / unit);
            // maxHp が unit の倍数なら末端目盛り(maxHp 自身)を除いて -1
            bool isExact = Mathf.Approximately(maxHp % unit, 0f);
            return isExact ? divisions - 1 : divisions;
        }

        /// <summary>
        /// index 番目（1始まり）の目盛り位置の比率 = index * unit / maxHp。
        /// </summary>
        public static float TickRatio(float maxHp, int index)
        {
            float unit = TickUnit(maxHp);
            return (index * unit) / maxHp;
        }
    }
}
