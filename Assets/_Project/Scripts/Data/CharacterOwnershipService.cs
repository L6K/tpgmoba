using Enigma.Character;

namespace Enigma.Data
{
    /// <summary>
    /// キャラクター所持状態の永続化レイヤー（暫定実装）。
    /// SQLite 導入後は owned_chars テーブルに置き換えること。
    /// </summary>
    public sealed class CharacterOwnershipService : ICharacterOwnership
    {
        // PlayerPrefs キーのプレフィックス。SQLite 移行時はこのプレフィックスで検索して削除できる
        private const string KeyPrefix = "owned_char_";

        private readonly ISaveStore _store;

        public CharacterOwnershipService(ISaveStore store)
        {
            _store = store;
        }

        public bool IsOwned(CharacterData data)
        {
            if (data == null) return false;
            return data.OwnedByDefault || _store.GetInt(KeyPrefix + data.CharId, 0) == 1;
        }

        public void Unlock(string charId)
        {
            _store.SetInt(KeyPrefix + charId, 1);
            _store.Save();
        }
    }
}
