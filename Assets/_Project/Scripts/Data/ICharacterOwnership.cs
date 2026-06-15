using Enigma.Character;

namespace Enigma.Data
{
    /// <summary>
    /// キャラクター所持状態の抽象。
    /// </summary>
    public interface ICharacterOwnership
    {
        bool IsOwned(CharacterData data);
        void Unlock(string charId);
    }
}
