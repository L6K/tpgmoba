using System.Collections.Generic;
using UnityEngine;
using Enigma.Character;

namespace Enigma.Data
{
    /// <summary>
    /// クリスタル残高とガチャ抽選を担当（暫定実装）。
    /// SQLite 導入後は gacha_log テーブルへの記録を追加すること。
    /// </summary>
    public sealed class GachaService : IGachaService
    {
        private const string KeyCrystals = "gacha_crystals";

        public const int SinglePullCost = 150;
        public const int TenPullCost    = 1500;

        private readonly ISaveStore        _store;
        private readonly ICharacterOwnership _ownership;
        private readonly IRandomSource     _random;

        private int _crystals;

        public int Crystals => _crystals;

        public GachaService(ISaveStore store, ICharacterOwnership ownership, IRandomSource random)
        {
            _store     = store;
            _ownership = ownership;
            _random    = random;
            _crystals  = _store.GetInt(KeyCrystals, 3000);
        }

        /// <summary>
        /// count（1 または 10）回ガチャを引く。
        /// 残高不足または pool が空の場合は false を返す。
        /// </summary>
        public bool TryPull(IReadOnlyList<CharacterData> pool, int count, List<PullResult> results)
        {
            if (pool == null) return false;

            int cost = count == 10 ? TenPullCost : SinglePullCost * count;

            // null 要素を除外した有効プールを構築
            var validPool = new List<CharacterData>();
            foreach (var c in pool)
            {
                if (c != null) validPool.Add(c);
            }

            if (validPool.Count == 0) return false;
            if (_crystals < cost)     return false;

            _crystals -= cost;
            _store.SetInt(KeyCrystals, _crystals);
            _store.Save();

            for (int i = 0; i < count; i++)
            {
                var chara = validPool[_random.Next(validPool.Count)];

                bool wasOwned = _ownership.IsOwned(chara);
                bool isNew    = !wasOwned;

                if (isNew)
                    _ownership.Unlock(chara.CharId);

                results.Add(new PullResult(chara, isNew));
            }

            return true;
        }
    }
}
