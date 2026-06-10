using System.Collections.Generic;
using UnityEngine;
using Enigma.Character;

namespace Enigma.Data
{
    /// <summary>
    /// クリスタル残高とガチャ抽選を担当（暫定実装）。
    /// SQLite 導入後は gacha_log テーブルへの記録を追加すること。
    /// </summary>
    public static class GachaService
    {
        // ── Keys ──────────────────────────────────────
        const string KEY_CRYSTALS = "gacha_crystals";

        // ── Costs ─────────────────────────────────────
        public const int SinglePullCost = 150;
        public const int TenPullCost    = 1500;

        // ── Crystals ──────────────────────────────────
        public static int Crystals
        {
            get => PlayerPrefs.GetInt(KEY_CRYSTALS, 3000);
            private set
            {
                PlayerPrefs.SetInt(KEY_CRYSTALS, value);
                PlayerPrefs.Save();
            }
        }

        // ── PullResult ────────────────────────────────
        public readonly struct PullResult
        {
            public readonly CharacterData character;
            public readonly bool isNew;

            public PullResult(CharacterData character, bool isNew)
            {
                this.character = character;
                this.isNew     = isNew;
            }
        }

        // ── TryPull ───────────────────────────────────
        /// <summary>
        /// count（1 または 10）回ガチャを引く。
        /// 残高不足または db が空の場合は false を返す。
        /// </summary>
        public static bool TryPull(CharacterDatabase db, int count, List<PullResult> results)
        {
            if (db == null || db.characters == null) return false;

            int cost = count == 10 ? TenPullCost : SinglePullCost * count;

            // 有効なキャラクターのみ対象
            var pool = new List<CharacterData>();
            foreach (var c in db.characters)
            {
                if (c != null) pool.Add(c);
            }

            if (pool.Count == 0) return false;
            if (Crystals < cost)  return false;

            Crystals -= cost;

            for (int i = 0; i < count; i++)
            {
                // 等確率抽選
                var chara = pool[Random.Range(0, pool.Count)];

                // 抽選 → 判定 → Unlock の順序を保つ
                bool wasOwned = CharacterOwnership.IsOwned(chara);
                bool isNew    = !wasOwned;

                if (isNew)
                    CharacterOwnership.Unlock(chara.charId);

                results.Add(new PullResult(chara, isNew));
            }

            return true;
        }
    }
}
