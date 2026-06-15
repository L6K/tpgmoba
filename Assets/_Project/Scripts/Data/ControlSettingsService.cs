using UnityEngine.InputSystem;

namespace Enigma.Data
{
    public sealed class ControlSettingsService : IControlSettingsService
    {
        private const string KeyCastMode    = "control_castmode";
        // v2_ プレフィックス: WASD 移動導入によりスキルを Q/E/R/None に変更した際に
        // 旧 Q/W/E/R 保存値を引き継がないようキープレフィックスを更新
        private const string KeySkillKeyFmt = "control_skillkey_v2_{0}";

        // デフォルトキー: Q/E/R（slot3 は将来枠・None）
        private static readonly Key[] DefaultSkillKeys =
        {
            Key.Q, Key.E, Key.R, Key.None
        };

        private readonly ISaveStore _store;

        public CastMode CastMode { get; private set; }

        public ControlSettingsService(ISaveStore store)
        {
            _store    = store;
            CastMode  = (CastMode)_store.GetInt(KeyCastMode, (int)CastMode.QuickWithIndicator);
        }

        public void SetCastMode(CastMode mode)
        {
            CastMode = mode;
            _store.SetInt(KeyCastMode, (int)mode);
            _store.Save();
        }

        public Key GetSkillKey(int slot)
        {
            if (slot < 0 || slot >= 4) return Key.None;
            int defaultVal = (int)DefaultSkillKeys[slot];
            return (Key)_store.GetInt(string.Format(KeySkillKeyFmt, slot), defaultVal);
        }

        public void SetSkillKey(int slot, Key key)
        {
            if (slot < 0 || slot >= 4) return;
            _store.SetInt(string.Format(KeySkillKeyFmt, slot), (int)key);
            _store.Save();
        }
    }
}
