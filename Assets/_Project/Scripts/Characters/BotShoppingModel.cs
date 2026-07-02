using System.Collections.Generic;

namespace Enigma.Character
{
    // カタログ1件分をショッピングモデル用に写した値。ItemData から Unity 非依存に変換して渡す。
    public readonly struct ShopItemInfo
    {
        public readonly int   Index;
        public readonly int   Price;
        public readonly float AttackPercent;
        public readonly float MaxHpBonus;
        public readonly float MoveSpeedPercent;

        public ShopItemInfo(int index, int price, float attackPercent, float maxHpBonus, float moveSpeedPercent)
        {
            Index            = index;
            Price            = price;
            AttackPercent    = attackPercent;
            MaxHpBonus       = maxHpBonus;
            MoveSpeedPercent = moveSpeedPercent;
        }
    }

    // Bot の「泉での買い物」判断。純 C#（UnityEngine 非依存）で EditMode テストしやすくする。
    public static class BotShoppingModel
    {
        private const int    MaxInventorySlots        = 6;
        private const float  MinRecallHpRatio          = 0.5f;
        private const float  MinRecallFountainDistance = 30f;
        private const int    MinRecallItemPrice        = 250;

        /// <summary>
        /// 次に買うべきカタログ添字を返す。買えない/枠満杯なら -1。
        /// preferHp なら MaxHpBonus>0 の品を優先、そうでなければ AttackPercent>0 の品を優先。
        /// 該当クラスに買える品が無ければ任意の買える品へフォールバック。
        /// 同クラス内では「買える中で最も高額」を選ぶ（大物買い）。
        /// </summary>
        public static int ChooseNextPurchase(IReadOnlyList<ShopItemInfo> catalog, int gold, int ownedCount, bool preferHp)
        {
            if (catalog == null || ownedCount >= MaxInventorySlots) return -1;

            int preferredBest = -1;
            int preferredBestPrice = -1;
            int fallbackBest = -1;
            int fallbackBestPrice = -1;

            for (int i = 0; i < catalog.Count; i++)
            {
                var item = catalog[i];
                if (item.Price <= 0 || item.Price > gold) continue;

                bool isPreferredClass = preferHp ? item.MaxHpBonus > 0f : item.AttackPercent > 0f;

                if (isPreferredClass)
                {
                    if (item.Price > preferredBestPrice)
                    {
                        preferredBestPrice = item.Price;
                        preferredBest = item.Index;
                    }
                }

                if (item.Price > fallbackBestPrice)
                {
                    fallbackBestPrice = item.Price;
                    fallbackBest = item.Index;
                }
            }

            return preferredBest >= 0 ? preferredBest : fallbackBest;
        }

        /// <summary>
        /// 買い物リコールを開始すべきか。
        /// 条件: 敵チャンピオンが近くにいない ∧ hpRatio >= 0.5 ∧ 泉まで distToFountain > 30
        ///       ∧ nextItemPrice > 0(買う物がある) ∧ gold >= nextItemPrice ∧ nextItemPrice >= 250(安物のために帰らない)
        /// </summary>
        public static bool ShouldRecallForShopping(float hpRatio, bool enemyNearby, int gold, int nextItemPrice, float distToFountain)
        {
            if (enemyNearby) return false;
            if (hpRatio < MinRecallHpRatio) return false;
            if (distToFountain <= MinRecallFountainDistance) return false;
            if (nextItemPrice <= 0) return false;
            if (gold < nextItemPrice) return false;
            if (nextItemPrice < MinRecallItemPrice) return false;
            return true;
        }
    }
}
