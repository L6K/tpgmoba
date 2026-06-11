using NUnit.Framework;
using UnityEngine;
using Enigma.Item;

namespace Enigma.Tests
{
    public sealed class ItemInventoryTests
    {
        private static ItemData MakeItem(string name, float attackPercent = 0f,
            float maxHpBonus = 0f, float moveSpeedPercent = 0f, int price = 0)
        {
            var item              = ScriptableObject.CreateInstance<ItemData>();
            item.ItemName         = name;
            item.AttackPercent    = attackPercent;
            item.MaxHpBonus       = maxHpBonus;
            item.MoveSpeedPercent = moveSpeedPercent;
            item.Price            = price;
            return item;
        }

        [Test]
        public void AddItem_UpTo6Slots_ReturnsTrue()
        {
            var inv = new ItemInventory();
            for (int i = 0; i < 6; i++)
                Assert.IsTrue(inv.AddItem(MakeItem($"Item{i}")));
            Assert.AreEqual(6, inv.Items.Count);
        }

        [Test]
        public void AddItem_7th_ReturnsFalse()
        {
            var inv = new ItemInventory();
            for (int i = 0; i < 6; i++)
                inv.AddItem(MakeItem($"Item{i}"));

            bool result = inv.AddItem(MakeItem("Item6"));

            Assert.IsFalse(result);
            Assert.AreEqual(6, inv.Items.Count);
        }

        [Test]
        public void AddItem_Null_ReturnsFalse()
        {
            var inv = new ItemInventory();
            Assert.IsFalse(inv.AddItem(null));
        }

        [Test]
        public void AttackMultiplier_SumsCorrectly()
        {
            var inv = new ItemInventory();
            inv.AddItem(MakeItem("Sword", attackPercent: 10f));
            inv.AddItem(MakeItem("Blade", attackPercent: 25f));

            // 1 + (10+25)/100 = 1.35
            Assert.AreEqual(1.35f, inv.AttackMultiplier, 0.001f);
        }

        [Test]
        public void TotalMaxHpBonus_SumsCorrectly()
        {
            var inv = new ItemInventory();
            inv.AddItem(MakeItem("Stone",   maxHpBonus: 50f));
            inv.AddItem(MakeItem("Belt",    maxHpBonus: 120f));
            inv.AddItem(MakeItem("StormSword", attackPercent: 35f, maxHpBonus: 60f));

            Assert.AreEqual(230f, inv.TotalMaxHpBonus, 0.001f);
        }

        [Test]
        public void MoveSpeedMultiplier_SumsCorrectly()
        {
            var inv = new ItemInventory();
            inv.AddItem(MakeItem("Boots", moveSpeedPercent: 12f));

            // 1 + 12/100 = 1.12
            Assert.AreEqual(1.12f, inv.MoveSpeedMultiplier, 0.001f);
        }

        [Test]
        public void Changed_FiredOnAddItem()
        {
            var inv = new ItemInventory();
            int fired = 0;
            inv.Changed += () => fired++;

            inv.AddItem(MakeItem("Item0"));

            Assert.AreEqual(1, fired);
        }
    }
}
