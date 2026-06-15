using UnityEngine;
using Enigma.Combat;
using Enigma.Item;

namespace Enigma.Character
{
    // プレイヤーのアイテムインベントリ管理と購入処理。
    public sealed class PlayerItems : MonoBehaviour
    {
        private ItemInventory _inventory;

        // 遅延初期化
        public ItemInventory Inventory => _inventory ??= new ItemInventory();

        // DamageUtility と PlayerController が参照するショートカット
        public float AttackMultiplier    => Inventory.AttackMultiplier;
        public float MoveSpeedMultiplier => Inventory.MoveSpeedMultiplier;

        // 空き枠チェックを先に行い、満杯なら出費しない
        public bool TryPurchase(ItemData item)
        {
            if (item == null) return false;

            // 満杯チェック（先に枠確認することで誤出費を防ぐ）
            if (Inventory.Items.Count >= 6) return false;

            var wallet = GetComponent<PlayerWallet>();
            if (wallet == null) return false;

            if (!wallet.Wallet.TrySpend(item.Price)) return false;

            Inventory.AddItem(item);

            // MaxHpBonus が付いているアイテムは装備時に即時 HP を増加
            if (item.MaxHpBonus > 0f)
            {
                var hc = GetComponent<HealthComponent>();
                if (hc != null)
                    hc.Model.AddMaxHp(item.MaxHpBonus);
            }

            return true;
        }
    }
}
