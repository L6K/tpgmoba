using System.Collections.Generic;
using UnityEngine;

namespace Enigma.Item
{
    // ショップUI が参照するアイテム一覧 SO。ビルダーが生成した ItemData を結線する。
    [CreateAssetMenu(fileName = "ItemShopCatalog", menuName = "Enigma/Item Shop Catalog")]
    public class ItemShopCatalog : ScriptableObject
    {
        public List<ItemData> Items = new();
    }
}
