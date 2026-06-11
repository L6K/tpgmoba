using UnityEngine;

namespace Enigma.Item
{
    [CreateAssetMenu(fileName = "Item_", menuName = "Enigma/Item Data")]
    public class ItemData : ScriptableObject
    {
        [Header("基本情報")]
        public string ItemName = "アイテム名";
        public int Price = 0;

        [Header("ステータス補正")]
        // 例: 10 = +10%
        public float AttackPercent = 0f;
        public float MaxHpBonus = 0f;
        public float MoveSpeedPercent = 0f;

        [Header("説明")]
        [TextArea]
        public string Description = "";

        [Header("見た目")]
        public Color ThemeColor = Color.white;
    }
}
