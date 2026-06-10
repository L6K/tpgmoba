using System.Collections.Generic;
using Enigma.Character;

namespace Enigma.Data
{
    /// <summary>
    /// ガチャサービスの抽象。
    /// </summary>
    public interface IGachaService
    {
        int Crystals { get; }

        /// <summary>
        /// count 回ガチャを引く。残高不足または pool が空の場合は false を返す。
        /// pool 内の null 要素はサービス側で除外する。
        /// </summary>
        bool TryPull(IReadOnlyList<CharacterData> pool, int count, List<PullResult> results);
    }

    /// <summary>
    /// 1回のガチャ結果。
    /// </summary>
    public readonly struct PullResult
    {
        public readonly CharacterData Character;
        public readonly bool IsNew;

        public PullResult(CharacterData character, bool isNew)
        {
            Character = character;
            IsNew     = isNew;
        }
    }
}
