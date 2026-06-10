using UnityEngine;
using Enigma.Character;

namespace Enigma.Data
{
    /// <summary>
    /// キャラクター所持状態の永続化レイヤー（暫定実装）。
    /// SQLite 導入後は owned_chars テーブルに置き換えること。
    /// </summary>
    public static class CharacterOwnership
    {
        // ── Keys ──────────────────────────────────────
        // PlayerPrefs キーのプレフィックス。SQLite 移行時はこのプレフィックスで検索して削除できる
        const string KEY_PREFIX = "owned_char_";

        public static bool IsOwned(CharacterData data)
        {
            if (data == null) return false;
            return data.ownedByDefault || PlayerPrefs.GetInt(KEY_PREFIX + data.charId, 0) == 1;
        }

        public static void Unlock(string charId)
        {
            PlayerPrefs.SetInt(KEY_PREFIX + charId, 1);
            PlayerPrefs.Save();
        }
    }
}
