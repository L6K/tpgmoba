using System.Collections.Generic;
using NUnit.Framework;
using Enigma.Character;

namespace Enigma.Tests
{
    public sealed class BotShoppingModelTests
    {
        private static List<ShopItemInfo> Catalog()
        {
            return new List<ShopItemInfo>
            {
                new ShopItemInfo(0, price: 300,  attackPercent: 10f, maxHpBonus: 0f,   moveSpeedPercent: 0f),
                new ShopItemInfo(1, price: 800,  attackPercent: 25f, maxHpBonus: 0f,   moveSpeedPercent: 0f),
                new ShopItemInfo(2, price: 500,  attackPercent: 0f,  maxHpBonus: 150f, moveSpeedPercent: 0f),
                new ShopItemInfo(3, price: 1200, attackPercent: 0f,  maxHpBonus: 400f, moveSpeedPercent: 0f),
                new ShopItemInfo(4, price: 400,  attackPercent: 0f,  maxHpBonus: 0f,   moveSpeedPercent: 8f),
            };
        }

        [Test]
        public void ChooseNextPurchase_AttackPreferred_PicksHighestAffordableAttackItem()
        {
            // gold=1000: attack 品は idx0(300)/idx1(800) が買える → 高額の idx1 を選ぶ
            int idx = BotShoppingModel.ChooseNextPurchase(Catalog(), gold: 1000, ownedCount: 0, preferHp: false);
            Assert.AreEqual(1, idx);
        }

        [Test]
        public void ChooseNextPurchase_PreferHp_PicksHighestAffordableHpItem()
        {
            // gold=2000: hp 品は idx2(500)/idx3(1200) が買える → 高額の idx3 を選ぶ
            int idx = BotShoppingModel.ChooseNextPurchase(Catalog(), gold: 2000, ownedCount: 0, preferHp: true);
            Assert.AreEqual(3, idx);
        }

        [Test]
        public void ChooseNextPurchase_PreferHp_NoAffordableHpItem_FallsBackToAnyAffordable()
        {
            // gold=350: hp 品は1つも買えない(500以上) → フォールバックで最高額の買える品(idx0:300)
            int idx = BotShoppingModel.ChooseNextPurchase(Catalog(), gold: 350, ownedCount: 0, preferHp: true);
            Assert.AreEqual(0, idx);
        }

        [Test]
        public void ChooseNextPurchase_AttackPreferred_NoAffordableAttackItem_FallsBackToAnyAffordable()
        {
            // gold=450: attack 品は1つも買えない(300は買えるが... 実は300は買える。450未満で attack を除外するため
            // カタログを絞ったケースで検証: 攻撃品が無いカタログを使う
            var catalog = new List<ShopItemInfo>
            {
                new ShopItemInfo(0, price: 400, attackPercent: 0f, maxHpBonus: 100f, moveSpeedPercent: 0f),
                new ShopItemInfo(1, price: 900, attackPercent: 20f, maxHpBonus: 0f,  moveSpeedPercent: 0f),
            };
            // gold=500: attack 品(idx1=900)は買えない → フォールバックで買える唯一の品 idx0
            int idx = BotShoppingModel.ChooseNextPurchase(catalog, gold: 500, ownedCount: 0, preferHp: false);
            Assert.AreEqual(0, idx);
        }

        [Test]
        public void ChooseNextPurchase_NotEnoughGold_ReturnsMinusOne()
        {
            int idx = BotShoppingModel.ChooseNextPurchase(Catalog(), gold: 100, ownedCount: 0, preferHp: false);
            Assert.AreEqual(-1, idx);
        }

        [Test]
        public void ChooseNextPurchase_InventoryFull_ReturnsMinusOne()
        {
            int idx = BotShoppingModel.ChooseNextPurchase(Catalog(), gold: 5000, ownedCount: 6, preferHp: false);
            Assert.AreEqual(-1, idx);
        }

        [Test]
        public void ShouldRecallForShopping_AllConditionsMet_ReturnsTrue()
        {
            bool result = BotShoppingModel.ShouldRecallForShopping(
                hpRatio: 0.8f, enemyNearby: false, gold: 1000, nextItemPrice: 500, distToFountain: 50f);
            Assert.IsTrue(result);
        }

        [Test]
        public void ShouldRecallForShopping_EnemyNearby_ReturnsFalse()
        {
            bool result = BotShoppingModel.ShouldRecallForShopping(
                hpRatio: 0.8f, enemyNearby: true, gold: 1000, nextItemPrice: 500, distToFountain: 50f);
            Assert.IsFalse(result);
        }

        [Test]
        public void ShouldRecallForShopping_LowHp_ReturnsFalse()
        {
            bool result = BotShoppingModel.ShouldRecallForShopping(
                hpRatio: 0.3f, enemyNearby: false, gold: 1000, nextItemPrice: 500, distToFountain: 50f);
            Assert.IsFalse(result);
        }

        [Test]
        public void ShouldRecallForShopping_NearFountain_ReturnsFalse()
        {
            bool result = BotShoppingModel.ShouldRecallForShopping(
                hpRatio: 0.8f, enemyNearby: false, gold: 1000, nextItemPrice: 500, distToFountain: 10f);
            Assert.IsFalse(result);
        }

        [Test]
        public void ShouldRecallForShopping_CheapItem_ReturnsFalse()
        {
            bool result = BotShoppingModel.ShouldRecallForShopping(
                hpRatio: 0.8f, enemyNearby: false, gold: 1000, nextItemPrice: 200, distToFountain: 50f);
            Assert.IsFalse(result);
        }

        [Test]
        public void ShouldRecallForShopping_NoItemToBuy_ReturnsFalse()
        {
            bool result = BotShoppingModel.ShouldRecallForShopping(
                hpRatio: 0.8f, enemyNearby: false, gold: 1000, nextItemPrice: -1, distToFountain: 50f);
            Assert.IsFalse(result);
        }

        [Test]
        public void ShouldRecallForShopping_NotEnoughGold_ReturnsFalse()
        {
            bool result = BotShoppingModel.ShouldRecallForShopping(
                hpRatio: 0.8f, enemyNearby: false, gold: 300, nextItemPrice: 500, distToFountain: 50f);
            Assert.IsFalse(result);
        }
    }
}
