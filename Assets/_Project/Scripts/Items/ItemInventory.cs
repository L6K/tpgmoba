using System;
using System.Collections.Generic;

namespace Enigma.Item
{
    // 試合内アイテムスロット管理。最大6枠。MonoBehaviour 非依存で EditMode テスト可能。
    public sealed class ItemInventory
    {
        private const int MaxSlots = 6;

        private readonly List<ItemData> _items = new(MaxSlots);

        public IReadOnlyList<ItemData> Items => _items;

        public event Action Changed;

        // null や満杯の場合は false を返して何もしない
        public bool AddItem(ItemData item)
        {
            if (item == null) return false;
            if (_items.Count >= MaxSlots) return false;

            _items.Add(item);
            Changed?.Invoke();
            return true;
        }

        // 全アイテムの AttackPercent 合算を乗数に変換
        public float AttackMultiplier
        {
            get
            {
                float total = 0f;
                foreach (var item in _items)
                    total += item.AttackPercent;
                return 1f + total / 100f;
            }
        }

        public float TotalMaxHpBonus
        {
            get
            {
                float total = 0f;
                foreach (var item in _items)
                    total += item.MaxHpBonus;
                return total;
            }
        }

        // 全アイテムの MoveSpeedPercent 合算を乗数に変換
        public float MoveSpeedMultiplier
        {
            get
            {
                float total = 0f;
                foreach (var item in _items)
                    total += item.MoveSpeedPercent;
                return 1f + total / 100f;
            }
        }
    }
}
