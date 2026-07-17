namespace Enigma.Combat
{
    /// <summary>
    /// ダメージゲート適用の純粋ロジック。UnityEngine 非依存。
    /// </summary>
    public static class DamageGating
    {
        /// <summary>
        /// ゲート未設定(null)または開(<see cref="IDamageGate.AllowsDamage"/>=true)なら
        /// amount をそのまま、閉なら 0 を返す。
        /// </summary>
        public static float Effective(float amount, IDamageGate gate)
        {
            return (gate == null || gate.AllowsDamage) ? amount : 0f;
        }
    }
}
