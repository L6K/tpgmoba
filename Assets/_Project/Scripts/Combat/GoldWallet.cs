using System;

namespace Enigma.Combat
{
    // 試合内ゴールドの残高管理。MonoBehaviour に依存しないため EditMode テスト可能。
    public sealed class GoldWallet
    {
        public int Gold { get; private set; }

        public event Action Changed;

        public GoldWallet(int initialGold)
        {
            Gold = initialGold;
        }

        public void Add(int amount)
        {
            Gold += amount;
            Changed?.Invoke();
        }

        // 残高不足の場合は false を返し残高を変化させない
        public bool TrySpend(int amount)
        {
            if (Gold < amount) return false;
            Gold -= amount;
            Changed?.Invoke();
            return true;
        }
    }
}
