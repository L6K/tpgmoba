namespace Enigma.Minion
{
    // ジャングルモンスターのリーシュ（キャンプへの帰還）判定を JungleMonster から抽出した純ロジック。
    // MonoBehaviour の Update から呼べるよう、距離(float)のみを引数に取る。
    public static class JungleLeashLogic
    {
        // キャンプ中心からこの距離を超えて追跡したら帰還を開始する。
        public const float LeashDistance = 15f;

        // 帰還先(キャンプ中心)からこの距離以内に入ったら帰還完了とみなす。
        public const float ReturnCompleteDistance = 2f;

        /// <summary>
        /// 戦闘中、キャンプ中心から distFromCamp 離れた地点にいるモンスターが
        /// 追跡を打ち切って帰還すべきかどうかを判定する。
        /// </summary>
        public static bool ShouldReturn(float distFromCamp)
        {
            return distFromCamp > LeashDistance;
        }

        /// <summary>
        /// 帰還中のモンスターがキャンプ中心から distFromCamp 離れた地点にいるとき、
        /// 帰還完了（位置スナップ・全回復・Idle 復帰）してよいかどうかを判定する。
        /// </summary>
        public static bool IsReturnComplete(float distFromCamp)
        {
            return distFromCamp <= ReturnCompleteDistance;
        }
    }
}
