namespace Enigma.Combat
{
    /// <summary>
    /// タイタン露出ゲート。レーン開通(自チームの1レーン分のタワー全滅)で露出するまで、
    /// 初期状態を含めてダメージを遮断する(<see cref="AllowsDamage"/>=false)。
    /// 露出状態は <see cref="TitanGuard"/> が定期的に <see cref="SetExposed"/> で更新する。
    /// </summary>
    public sealed class TitanDamageGate : IDamageGate
    {
        // 初期は露出前=閉(タイタンは自チームタワーに守られている)。
        public bool AllowsDamage { get; private set; }

        /// <summary>
        /// 露出状態を設定する。状態が変化したとき true を返す(遷移時ログ用)。
        /// </summary>
        public bool SetExposed(bool exposed)
        {
            if (AllowsDamage == exposed) return false;
            AllowsDamage = exposed;
            return true;
        }
    }
}
