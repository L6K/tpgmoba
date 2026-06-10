using Enigma.Data;

namespace Enigma.Ability
{
    public enum CastAction { None, ShowIndicator, Cast, Cancel }

    /// <summary>
    /// スキルのキャストモード状態機械。MonoBehaviour に依存しないため EditMode テスト可能。
    /// </summary>
    public sealed class CastModeLogic
    {
        private CastMode _mode;

        public int ArmedSlot { get; private set; } = -1;

        public CastModeLogic(CastMode mode)
        {
            _mode = mode;
        }

        /// <summary>実行時に CastMode が変わった場合は同期して再構成する。</summary>
        public void SyncMode(CastMode mode)
        {
            if (_mode == mode) return;
            _mode = mode;
            ArmedSlot = -1; // モード変更でアーム解除
        }

        /// <param name="isInstant">Targeted スキル（対象指定）は方式問わず即発動</param>
        public CastAction HandleKeyDown(int slot, bool isInstant)
        {
            if (isInstant) return CastAction.Cast;

            switch (_mode)
            {
                case CastMode.Quick:
                    return CastAction.Cast;

                case CastMode.QuickWithIndicator:
                    ArmedSlot = slot;
                    return CastAction.ShowIndicator;

                case CastMode.Normal:
                    // 別スロット押下なら切替、同スロットもアーム
                    ArmedSlot = slot;
                    return CastAction.ShowIndicator;

                default:
                    return CastAction.None;
            }
        }

        /// <summary>キー離し時のアクション（QuickWithIndicator でキーを離したら発動）</summary>
        public CastAction HandleKeyUp(int slot)
        {
            if (_mode == CastMode.QuickWithIndicator && ArmedSlot == slot)
            {
                ArmedSlot = -1;
                return CastAction.Cast;
            }

            return CastAction.None;
        }

        /// <summary>Normal モードの確定（左クリック）</summary>
        public CastAction HandleConfirm()
        {
            if (_mode == CastMode.Normal && ArmedSlot >= 0)
            {
                ArmedSlot = -1;
                return CastAction.Cast;
            }

            return CastAction.None;
        }

        /// <summary>キャンセル（ESC / 右クリック）</summary>
        public CastAction HandleCancel()
        {
            if (ArmedSlot >= 0)
            {
                ArmedSlot = -1;
                return CastAction.Cancel;
            }

            return CastAction.None;
        }
    }
}
