using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Enigma.Character;
using Enigma.Data;

namespace Enigma.Tests
{
    public class GachaServiceTests
    {
        // ── ヘルパー ──────────────────────────────────────

        private static CharacterData MakeChar(string charId, bool ownedByDefault = false)
        {
            var data = ScriptableObject.CreateInstance<CharacterData>();
            data.CharId         = charId;
            data.DisplayName    = charId;
            data.OwnedByDefault = ownedByDefault;
            return data;
        }

        private static (GachaService gacha, CharacterOwnershipService ownership, FakeSaveStore store)
            Build(int initialCrystals, FakeRandomSource random, CharacterData[] chars = null)
        {
            var store     = new FakeSaveStore();
            store.SetInt("gacha_crystals", initialCrystals);
            var ownership = new CharacterOwnershipService(store);
            var gacha     = new GachaService(store, ownership, random);
            return (gacha, ownership, store);
        }

        // ── 残高不足 ───────────────────────────────────────

        [Test]
        public void TryPull_InsufficientCrystals_ReturnsFalse_AndBalanceUnchanged()
        {
            var chara  = MakeChar("a");
            var (gacha, _, _) = Build(100, new FakeRandomSource(0));
            var results = new List<PullResult>();

            bool ok = gacha.TryPull(new[] { chara }, 1, results);

            Assert.IsFalse(ok);
            Assert.AreEqual(100, gacha.Crystals);
            Assert.AreEqual(0, results.Count);
        }

        // ── 単発成功・残高減算 ─────────────────────────────

        [Test]
        public void TryPull_SinglePull_DecreasesCrystalsBySinglePullCost()
        {
            var chara  = MakeChar("b");
            var (gacha, _, _) = Build(3000, new FakeRandomSource(0));
            var results = new List<PullResult>();

            bool ok = gacha.TryPull(new[] { chara }, 1, results);

            Assert.IsTrue(ok);
            Assert.AreEqual(3000 - GachaService.SinglePullCost, gacha.Crystals);
        }

        [Test]
        public void TryPull_TenPull_DecreasesCrystalsByTenPullCost()
        {
            var chars   = new CharacterData[3];
            for (int i = 0; i < chars.Length; i++) chars[i] = MakeChar($"c{i}");

            // 10連：index 0,1,2 を繰り返す
            var random = new FakeRandomSource(0, 1, 2, 0, 1, 2, 0, 1, 2, 0);
            var (gacha, _, _) = Build(3000, random);
            var results = new List<PullResult>();

            bool ok = gacha.TryPull(chars, 10, results);

            Assert.IsTrue(ok);
            Assert.AreEqual(3000 - GachaService.TenPullCost, gacha.Crystals);
            Assert.AreEqual(10, results.Count);
        }

        // ── 未所持キャラ → IsNew / Unlock ─────────────────

        [Test]
        public void TryPull_NewCharacter_IsNewTrueAndUnlocked()
        {
            var chara  = MakeChar("d");
            var (gacha, ownership, _) = Build(3000, new FakeRandomSource(0));
            var results = new List<PullResult>();

            gacha.TryPull(new[] { chara }, 1, results);

            Assert.IsTrue(results[0].IsNew);
            Assert.IsTrue(ownership.IsOwned(chara));
        }

        // ── 所持済みキャラ → IsNew == false ───────────────

        [Test]
        public void TryPull_AlreadyOwnedCharacter_IsNewFalse()
        {
            var chara  = MakeChar("e", ownedByDefault: true);
            var (gacha, _, _) = Build(3000, new FakeRandomSource(0));
            var results = new List<PullResult>();

            gacha.TryPull(new[] { chara }, 1, results);

            Assert.IsFalse(results[0].IsNew);
        }

        // ── 同一10連内で同キャラ2回 ───────────────────────

        [Test]
        public void TryPull_SameCharTwiceInTenPull_FirstIsNew_SecondIsDuplicate()
        {
            var chara = MakeChar("f");
            var pool  = new CharacterData[] { chara };

            // 0番を2回返す（他は 0 のまま）
            var random = new FakeRandomSource(0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
            var (gacha, _, _) = Build(3000, random);
            var results = new List<PullResult>();

            gacha.TryPull(pool, 10, results);

            Assert.IsTrue(results[0].IsNew,  "1回目は NEW");
            Assert.IsFalse(results[1].IsNew, "2回目は重複（既に Unlock 済み）");
        }
    }
}
